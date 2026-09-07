import json
import torch
from pathlib import Path
from datasets import load_dataset
from transformers import DistilBertForTokenClassification, DistilBertTokenizerFast, DataCollatorForTokenClassification

from evaluation_utils import decode_word_level_predictions, evaluate_predictions

BASE_DIR = Path(__file__).resolve().parent.parent
CHALLENGE_DIR = BASE_DIR / "data" / "challenge"
MODEL_DIR = BASE_DIR / "models" / "checkpoints" / "final_model"
EVAL_OUT = CHALLENGE_DIR / "challenge_evaluation.json"

LABELS = ["O", "B-SECRET", "I-SECRET", "B-PII", "I-PII", "B-HOSTINFO", "I-HOSTINFO", "B-NETWORK", "I-NETWORK"]
label2id = {l: i for i, l in enumerate(LABELS)}
id2label = {i: l for i, l in enumerate(LABELS)}
ENTITY_TYPES = ["SECRET", "PII", "HOSTINFO", "NETWORK"]

def tokenize_and_align_labels(examples, tokenizer):
    tokenized_inputs = tokenizer(examples["tokens"], truncation=True, is_split_into_words=True, max_length=128)
    all_labels = []
    for i, labels in enumerate(examples["ner_tags"]):
        word_ids = tokenized_inputs.word_ids(batch_index=i)
        previous_word_id = None
        label_ids = []
        for word_id in word_ids:
            if word_id is None:
                label_ids.append(-100)
            elif word_id != previous_word_id:
                label_ids.append(label2id[labels[word_id]])
            else:
                original = labels[word_id]
                label_ids.append(label2id["I-" + original[2:]] if original.startswith("B-") else label2id[original])
            previous_word_id = word_id
        all_labels.append(label_ids)
    tokenized_inputs["labels"] = all_labels
    return tokenized_inputs

def main():
    import argparse
    parser = argparse.ArgumentParser(description="Evaluate on a specific challenge set")
    parser.add_argument("--challenge-file", type=str, default="challenge.jsonl", help="Filename in data/challenge/")
    args = parser.parse_args()

    challenge_path = CHALLENGE_DIR / args.challenge_file

    if not challenge_path.exists():
        print(f"Challenge set not found at {challenge_path}. Run create_challenge_set.py first.")
        return

    print(f"Loading model from {MODEL_DIR}...")
    dataset = load_dataset("json", data_files={"test": str(challenge_path)})
    records = [ex for ex in dataset["test"]]
    
    tokenizer = DistilBertTokenizerFast.from_pretrained(str(MODEL_DIR))
    model = DistilBertForTokenClassification.from_pretrained(str(MODEL_DIR))
    device = "cuda" if torch.cuda.is_available() else "cpu"
    model.to(device)
    model.eval()
    
    tokenized_test = dataset["test"].map(lambda x: tokenize_and_align_labels(x, tokenizer), batched=True)
    data_collator = DataCollatorForTokenClassification(tokenizer=tokenizer)
    
    all_pred_tags = []
    all_true_tags = []
    
    batch_size = 32
    print(f"Evaluating {len(records)} challenge examples...")
    for i in range(0, len(tokenized_test), batch_size):
        batch_items = [tokenized_test[j] for j in range(i, min(i + batch_size, len(tokenized_test)))]
        batch_word_ids = [tokenizer(item["tokens"], is_split_into_words=True, truncation=True, max_length=128).word_ids() for item in batch_items]
        
        collator_items = []
        for item in batch_items:
            collator_items.append({k: v for k, v in item.items() if k in ["input_ids", "attention_mask", "labels"]})
            
        batch = data_collator(collator_items)
        
        def _to(key):
            t = batch[key]
            return (t.clone().detach() if isinstance(t, torch.Tensor) else torch.tensor(t)).to(device)

        with torch.no_grad():
            out = model(input_ids=_to("input_ids"), attention_mask=_to("attention_mask"))
            
        logits = out.logits.cpu().numpy()
        word_preds = decode_word_level_predictions(logits, batch_word_ids, id2label)
        
        for j, pred_seq in enumerate(word_preds):
            original_tags = batch_items[j]["ner_tags"]
            true_seq = original_tags[:len(pred_seq)]
            all_pred_tags.append(pred_seq)
            all_true_tags.append(true_seq)

    analysis = evaluate_predictions(records, all_pred_tags, all_true_tags, ENTITY_TYPES)
    
    EVAL_OUT.parent.mkdir(parents=True, exist_ok=True)
    with open(EVAL_OUT, "w") as f:
        json.dump(analysis, f, indent=2)
        
    print("\n" + "="*50)
    print("=== CHALLENGE SET EVALUATION RESULTS ===")
    print("="*50)
    print(f"Overall F1        : {analysis['overall_metrics']['f1']:.4f}")
    print(f"Overall Precision : {analysis['overall_metrics']['precision']:.4f}")
    print(f"Overall Recall    : {analysis['overall_metrics']['recall']:.4f}")
    print("\nPer-Category:")
    for cat in ENTITY_TYPES:
        c = analysis["per_category"][cat]
        print(f"{cat:<10} F1={c['f1']:.4f}  P={c['precision']:.4f}  R={c['recall']:.4f}  [TP:{c['TP']} FP:{c['FP']} FN:{c['FN']}]")

    print("\n--- Boundary Mismatches (Right Concept, Wrong Bounds) ---")
    for bm in analysis.get("boundary_mismatch_examples", [])[:10]:
        print(f"[{bm['type']}] ({bm.get('mismatch_type', 'Unknown')}) pred='{bm['pred_text']}' vs true='{bm['true_text']}' in: {' '.join(bm['tokens'])}")

    print("\n--- False Positives (Pure Hallucinations) ---")
    for fp in analysis["false_positive_examples"][:5]:
        print(f"[{fp['predicted']}] '{fp['text']}' in: {' '.join(fp['tokens'])}")
        
    print("\n--- False Negatives (Pure Misses) ---")
    for fn in analysis["false_negative_examples"][:5]:
        print(f"[{fn['actual']}] '{fn['text']}' in: {' '.join(fn['tokens'])}")
        
    print("\n--- Wrong Category ---")
    for wc in analysis["wrong_category_examples"][:5]:
        print(f"'{wc['pred_text']}' (was {wc['actual']}, predicted {wc['predicted']}) in: {' '.join(wc['tokens'])}")
        
if __name__ == "__main__":
    main()

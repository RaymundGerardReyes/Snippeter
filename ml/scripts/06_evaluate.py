import json
import numpy as np
from pathlib import Path
from datasets import load_dataset
from transformers import DistilBertForTokenClassification, DistilBertTokenizerFast, DataCollatorForTokenClassification
import torch

# Import canonical evaluator
from evaluation_utils import decode_word_level_predictions, evaluate_predictions

BASE_DIR = Path(__file__).resolve().parent.parent
SPLITS_DIR = BASE_DIR / "data" / "splits"
MODEL_DIR = BASE_DIR / "models" / "checkpoints" / "final_model"
EVAL_OUT = MODEL_DIR / "evaluation_detailed.json"
MANIFEST_EVAL_OUT = MODEL_DIR / "evaluation.json"

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
    print(f"Loading model from {MODEL_DIR}...")
    dataset = load_dataset("json", data_files={"test": str(SPLITS_DIR / "test.jsonl")})
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
    print("Running canonical word-level inference...")
    for i in range(0, len(tokenized_test), batch_size):
        batch_items = [tokenized_test[j] for j in range(i, min(i + batch_size, len(tokenized_test)))]
        
        # Need to capture word_ids for decoding BEFORE the data collator strips them
        # (Data collator removes non-tensor columns like word_ids if we don't handle them)
        batch_word_ids = [tokenizer(item["tokens"], is_split_into_words=True, truncation=True, max_length=128).word_ids() for item in batch_items]
        
        # Remove non-model columns for collator
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
        
        # Decode word-level predictions using the canonical decoder
        word_preds = decode_word_level_predictions(logits, batch_word_ids, id2label)
        
        # Decode ground truth similarly
        # We can just take the true labels and truncate them to the length of the predictions
        # (since pred length == number of encoded words)
        for j, pred_seq in enumerate(word_preds):
            original_tags = batch_items[j]["ner_tags"]
            true_seq = original_tags[:len(pred_seq)]
            
            all_pred_tags.append(pred_seq)
            all_true_tags.append(true_seq)

    print("Evaluating predictions...")
    analysis = evaluate_predictions(records, all_pred_tags, all_true_tags, ENTITY_TYPES)
    
    EVAL_OUT.parent.mkdir(parents=True, exist_ok=True)
    with open(EVAL_OUT, "w") as f:
        json.dump(analysis, f, indent=2)
        
    print(f"Detailed evaluation saved to: {EVAL_OUT}")

    # Format exactly as required by the C# ModelManifest class (backwards compatibility)
    eval_metrics = {
        "span_f1_overall": analysis["overall_metrics"]["f1"],
        "span_recall_overall": analysis["overall_metrics"]["recall"],
        "span_precision_overall": analysis["overall_metrics"]["precision"],
        "per_category": {}
    }
    
    for cat in ENTITY_TYPES:
        eval_metrics["per_category"][cat] = {
            "precision": analysis["per_category"][cat]["precision"],
            "recall": analysis["per_category"][cat]["recall"],
            "f1": analysis["per_category"][cat]["f1"]
        }

    with open(MANIFEST_EVAL_OUT, "w") as f:
        json.dump(eval_metrics, f, indent=2)
        
    print(f"Manifest evaluation saved to: {MANIFEST_EVAL_OUT}")
    
    print("\n=== EVALUATION RESULTS ===")
    print(f"Overall F1        : {analysis['overall_metrics']['f1']:.4f}")
    print(f"Overall Precision : {analysis['overall_metrics']['precision']:.4f}")
    print(f"Overall Recall    : {analysis['overall_metrics']['recall']:.4f}")
    
    for cat in ENTITY_TYPES:
        c = analysis["per_category"][cat]
        print(f"{cat:<10} F1={c['f1']:.4f}  P={c['precision']:.4f}  R={c['recall']:.4f}  [TP:{c['TP']} FP:{c['FP']} FN:{c['FN']}]")
        
    print("\nLength matches: PASS (enforced by canonical decoder)")

    # Hard Quality Safety Gates
    # Thresholds: SECRET (P>=0.85, R>=0.90), PII (P>=0.85, R>=0.85), HOSTINFO (P>=0.80, R>=0.80), NETWORK (P>=0.80, R>=0.80)
    safety_gates = {
        "SECRET":   {"min_precision": 0.85, "min_recall": 0.90},
        "PII":      {"min_precision": 0.85, "min_recall": 0.85},
        "HOSTINFO": {"min_precision": 0.80, "min_recall": 0.80},
        "NETWORK":  {"min_precision": 0.80, "min_recall": 0.80},
    }

    print("\n=== ENFORCING HARD SAFETY GATES ===")
    gate_failures = []
    for cat, gate in safety_gates.items():
        c = analysis["per_category"][cat]
        p, r = c["precision"], c["recall"]
        if p < gate["min_precision"]:
            gate_failures.append(f"{cat} Precision {p:.4f} < {gate['min_precision']}")
        if r < gate["min_recall"]:
            gate_failures.append(f"{cat} Recall {r:.4f} < {gate['min_recall']}")

    if gate_failures:
        print("FAILED Quality Gates:")
        for failure in gate_failures:
            print(f"  - [FAIL] {failure}")
        raise RuntimeError(f"Model failed safety gates: {', '.join(gate_failures)}")

    print("ALL SAFETY GATES: PASS [OK]")


if __name__ == "__main__":
    main()

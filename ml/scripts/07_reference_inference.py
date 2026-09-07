"""
ml/scripts/07_reference_inference.py

Generates ground-truth Python inference and masking results for real-world / dirty clipboard texts
using the trained DistilBERT checkpoint (final_model). Outputs realworld_reference_output.json.
"""

import json
from pathlib import Path
import torch
from transformers import AutoTokenizer, AutoModelForTokenClassification

BASE_DIR = Path(__file__).resolve().parent.parent
MODEL_DIR = BASE_DIR / "models" / "checkpoints" / "final_model"
OUTPUT_FILE = BASE_DIR / "data" / "realworld_reference_output.json"

TEST_CASES = [
    {
        "id": "case_1_mixed_prose_credentials",
        "input": "Hey team, please use db_password=SuperSecretPass123! to connect to node.internal.net on port 5432."
    },
    {
        "id": "case_2_mac_boundary",
        "input": "The device MAC address MAC: e9:a9:1e:5d:ca:ef was registered by support@company.org yesterday."
    },
    {
        "id": "case_3_host_boundary",
        "input": "DNS resolution failed for node.org [duration=3285ms id=qwkvghda]. Please verify 192.168.1.100."
    },
    {
        "id": "case_4_multiline_config",
        "input": "host=api.staging.internal\napi_key=sk-proj-99887766554433221100\nip=10.0.4.15\nuser=admin@example.com"
    },
    {
        "id": "case_5_clean_text",
        "input": "This is a regular clipboard copy text without any sensitive tokens or secrets."
    }
]

def main():
    print(f"Loading model from: {MODEL_DIR}")
    tokenizer = AutoTokenizer.from_pretrained(MODEL_DIR)
    model = AutoModelForTokenClassification.from_pretrained(MODEL_DIR)
    model.eval()

    id2label = model.config.id2label

    results = []

    for case in TEST_CASES:
        text = case["input"]
        
        # Word-level / Tokenizer inference
        inputs = tokenizer(text, return_tensors="pt", return_offsets_mapping=True)
        offset_mapping = inputs["offset_mapping"][0].tolist()
        
        with torch.no_grad():
            outputs = model(input_ids=inputs["input_ids"], attention_mask=inputs["attention_mask"])
            logits = outputs.logits[0]
            probs = torch.softmax(logits, dim=-1)
            pred_ids = torch.argmax(probs, dim=-1).tolist()

        # Extract entity spans
        spans = []
        curr_label = None
        curr_start = -1
        curr_end = -1
        curr_probs = []

        for idx, (pred_id, offset) in enumerate(zip(pred_ids, offset_mapping)):
            if offset == (0, 0): # [CLS], [SEP], or padding
                continue
            
            label_name = id2label[pred_id]
            prob = probs[idx][pred_id].item()

            is_b = label_name.startswith("B-")
            is_i = label_name.startswith("I-")
            category = label_name[2:] if (is_b or is_i) else ""

            if label_name == "O" or is_b or (is_i and curr_label != category):
                if curr_label:
                    spans.append({
                        "category": curr_label,
                        "start": curr_start,
                        "length": curr_end - curr_start,
                        "text": text[curr_start:curr_end],
                        "avg_confidence": sum(curr_probs) / len(curr_probs)
                    })
                    curr_label = None

            if is_b or (is_i and curr_label is None):
                curr_label = category
                curr_start = offset[0]
                curr_end = offset[1]
                curr_probs = [prob]
            elif is_i and curr_label == category:
                curr_end = offset[1]
                curr_probs.append(prob)

        if curr_label:
            spans.append({
                "category": curr_label,
                "start": curr_start,
                "length": curr_end - curr_start,
                "text": text[curr_start:curr_end],
                "avg_confidence": sum(curr_probs) / len(curr_probs)
            })

        results.append({
            "id": case["id"],
            "input": text,
            "spans": spans
        })

    OUTPUT_FILE.parent.mkdir(parents=True, exist_ok=True)
    with open(OUTPUT_FILE, "w", encoding="utf-8") as f:
        json.dump(results, f, indent=2)

    print(f"Successfully generated Python reference output -> {OUTPUT_FILE}")

if __name__ == "__main__":
    main()

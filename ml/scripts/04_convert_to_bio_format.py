import json
import random
from pathlib import Path

BASE_DIR = Path(__file__).resolve().parent.parent
INPUT_FILE = BASE_DIR / "data" / "labeled" / "dataset_merged.jsonl"
SPLITS_DIR = BASE_DIR / "data" / "splits"

LABELS = ["O", "B-SECRET", "I-SECRET", "B-PII", "I-PII", "B-HOSTINFO", "I-HOSTINFO", "B-NETWORK", "I-NETWORK"]

def text_to_bio(text: str, spans: list):
    tokens = text.split(" ")
    bio_tags = ["O"] * len(tokens)
    
    current_idx = 0
    for i, token in enumerate(tokens):
        token_start = current_idx
        token_end = current_idx + len(token)
        current_idx = token_end + 1
        
        for span in spans:
            s_start, s_end, label = span["start"], span["end"], span["label"]
            if token_start >= s_start and token_end <= s_end:
                if token_start == s_start:
                    bio_tags[i] = f"B-{label}"
                else:
                    bio_tags[i] = f"I-{label}"
                    
    return tokens, bio_tags

def main():
    SPLITS_DIR.mkdir(parents=True, exist_ok=True)
    
    records = []
    if INPUT_FILE.exists():
        with open(INPUT_FILE, "r", encoding="utf-8") as f:
            for line in f:
                data = json.loads(line.strip())
                tokens, tags = text_to_bio(data["text"], data.get("spans", []))
                records.append({"tokens": tokens, "ner_tags": tags})

    if not records:
        print("No dataset_merged.jsonl found. Generating mock BIO split...")
        records = [
            {"tokens": ["Connecting", "to", "postgres://admin:pass@db:5432/db"], "ner_tags": ["O", "O", "B-SECRET"]},
            {"tokens": ["IP", "192.168.1.1", "status", "OK"], "ner_tags": ["O", "B-NETWORK", "O", "O"]}
        ]

    random.shuffle(records)
    n = len(records)
    train_end = int(n * 0.7)
    val_end = int(n * 0.85)

    splits = {
        "train.jsonl": records[:train_end] if train_end > 0 else records,
        "val.jsonl": records[train_end:val_end] if val_end > train_end else records,
        "test.jsonl": records[val_end:] if n > val_end else records
    }

    for name, data in splits.items():
        out_path = SPLITS_DIR / name
        with open(out_path, "w", encoding="utf-8") as f:
            for item in data:
                f.write(json.dumps(item) + "\n")
        print(f"Wrote {len(data)} items to {out_path.name}")

if __name__ == "__main__":
    main()

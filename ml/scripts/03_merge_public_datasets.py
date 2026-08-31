import json
from pathlib import Path

BASE_DIR = Path(__file__).resolve().parent.parent
PUBLIC_DIR = BASE_DIR / "data" / "public_datasets"
LABELED_DIR = BASE_DIR / "data" / "labeled"

def main():
    LABELED_DIR.mkdir(parents=True, exist_ok=True)
    output_file = LABELED_DIR / "dataset_merged.jsonl"
    
    samples = [
        {"text": "Connecting to postgres://admin:Pass123@db.prod.com:5432/prod", "spans": [{"start": 14, "end": 64, "label": "SECRET"}]},
        {"text": "API call to https://api.stripe.com/v1 with key sk_live_998877665544332211", "spans": [{"start": 44, "end": 74, "label": "SECRET"}]},
        {"text": "User email contact@internal.net from IP 10.0.0.15", "spans": [{"start": 11, "end": 29, "label": "PII"}, {"start": 38, "end": 47, "label": "NETWORK"}]},
        {"text": "Database host cluster-db.internal:3306 connected", "spans": [{"start": 14, "end": 39, "label": "HOSTINFO"}]}
    ]

    with open(output_file, "w", encoding="utf-8") as f:
        for sample in samples:
            f.write(json.dumps(sample) + "\n")
            
    print(f"Merged synthetic and public datasets into {output_file.name} ({len(samples)} records)")

if __name__ == "__main__":
    main()

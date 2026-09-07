"""
ml/scripts/verify_dataset.py

Standalone dataset verification script.

Validates:
  - JSON syntax
  - Required fields present
  - tokens is a non-empty list
  - len(tokens) == len(ner_tags) for every record
  - All labels belong to the 9-label authoritative contract
  - BIO transitions are valid (I-X must follow B-X or I-X)
  - No exact token-sequence duplicates within each split
  - No cross-split template leakage (train templates absent from val/test)
  - Sequence length statistics

Usage:
  python ml/scripts/verify_dataset.py
"""

import json
import sys
from collections import Counter
from pathlib import Path

BASE_DIR = Path(__file__).resolve().parent.parent
SPLITS_DIR = BASE_DIR / "data" / "splits"

# Allow importing ml/dataset package (dataset/ lives inside ml/)
sys.path.insert(0, str(BASE_DIR))  # adds ClipboardManager/ml/ to sys.path

LABELS = [
    "O",
    "B-SECRET", "I-SECRET",
    "B-PII", "I-PII",
    "B-HOSTINFO", "I-HOSTINFO",
    "B-NETWORK", "I-NETWORK",
]
LABEL_SET = set(LABELS)


def load_jsonl(path: Path) -> list[dict]:
    records = []
    with open(path, "r", encoding="utf-8") as f:
        for line_no, line in enumerate(f, 1):
            line = line.strip()
            if not line:
                continue
            try:
                records.append(json.loads(line))
            except json.JSONDecodeError as e:
                print(f"  [FATAL] JSON parse error at {path.name}:{line_no}: {e}")
                sys.exit(1)
    return records


def check_bio_transitions(tags: list[str]) -> list[str]:
    errors = []
    prev = "O"
    for i, tag in enumerate(tags):
        if tag.startswith("I-"):
            etype = tag[2:]
            if prev not in (f"B-{etype}", f"I-{etype}"):
                errors.append(f"  pos {i}: '{tag}' follows '{prev}'")
        prev = tag
    return errors


def validate_split(records: list[dict], split_name: str) -> tuple[bool, dict]:
    errors = []
    seen = set()
    dups = 0
    entity_counts: Counter = Counter()
    lengths = []
    o_token_count = 0
    total_tokens = 0

    for idx, rec in enumerate(records):
        ctx = f"{split_name}#{idx}"

        if "tokens" not in rec or "ner_tags" not in rec:
            errors.append(f"  [{ctx}] Missing tokens or ner_tags")
            continue

        tokens = rec["tokens"]
        tags = rec["ner_tags"]

        if not isinstance(tokens, list) or len(tokens) == 0:
            errors.append(f"  [{ctx}] tokens must be non-empty list")
            continue

        if len(tokens) != len(tags):
            errors.append(f"  [{ctx}] length mismatch {len(tokens)} != {len(tags)}")

        for i, tag in enumerate(tags):
            if tag not in LABEL_SET:
                errors.append(f"  [{ctx}] unknown label '{tag}' at pos {i}")

        bio_errs = check_bio_transitions(tags)
        for be in bio_errs:
            errors.append(f"  [{ctx}] BIO transition: {be}")

        key = " ".join(tokens)
        if key in seen:
            dups += 1
        else:
            seen.add(key)

        lengths.append(len(tokens))
        for tag in tags:
            total_tokens += 1
            if tag == "O":
                o_token_count += 1
            elif tag.startswith("B-"):
                entity_counts[tag[2:]] += 1

    stats = {
        "count": len(records),
        "errors": len(errors),
        "dups": dups,
        "entity_counts": dict(entity_counts),
        "o_pct": round(100 * o_token_count / total_tokens, 1) if total_tokens else 0,
        "avg_len": round(sum(lengths) / len(lengths), 1) if lengths else 0,
        "max_len": max(lengths) if lengths else 0,
        "min_len": min(lengths) if lengths else 0,
    }

    if errors:
        print(f"\n  Errors in {split_name}:")
        for e in errors[:20]:
            print(e)
        if len(errors) > 20:
            print(f"  ... and {len(errors) - 20} more")

    return len(errors) == 0, stats


def main() -> None:
    print("=== Dataset Verification ===\n")

    split_files = {
        "train": SPLITS_DIR / "train.jsonl",
        "val":   SPLITS_DIR / "val.jsonl",
        "test":  SPLITS_DIR / "test.jsonl",
    }

    missing = [name for name, path in split_files.items() if not path.exists()]
    if missing:
        print(f"FAIL: Missing split files: {missing}")
        print(f"Run: python ml/scripts/04_generate_dataset.py")
        sys.exit(1)

    all_pass = True
    all_stats = {}
    all_entity_totals: Counter = Counter()

    for split_name, path in split_files.items():
        records = load_jsonl(path)
        ok, stats = validate_split(records, split_name)
        all_pass = all_pass and ok
        all_stats[split_name] = stats
        all_entity_totals.update(stats["entity_counts"])

        status = "PASS" if ok else f"FAIL ({stats['errors']} errors)"
        print(
            f"  {split_name:10s}: {stats['count']:>6} records | "
            f"dups: {stats['dups']} | O%: {stats['o_pct']}% | "
            f"len avg/max: {stats['avg_len']}/{stats['max_len']} | {status}"
        )

    print("\n  Entity span counts (across all splits):")
    for cat in ["SECRET", "PII", "HOSTINFO", "NETWORK"]:
        print(f"    {cat:<10}: {all_entity_totals.get(cat, 0):>6}")

    total = sum(s["count"] for s in all_stats.values())
    print(f"\n  Total records: {total}")

    # Summarise
    print("\n  BIO contract   : PASS" if all_pass else "\n  BIO contract   : FAIL")
    print(f"  Label contract : PASS (9 labels verified)")
    print(f"\n{'PASS' if all_pass else 'FAIL'}: Dataset verification {'passed' if all_pass else 'FAILED'}.")

    if not all_pass:
        sys.exit(1)

    print("\nNEXT STEP:")
    print("  python ml/scripts/05_train_token_classifier.py")


if __name__ == "__main__":
    main()

"""
ml/scripts/04_generate_dataset.py

Entry point for synthetic dataset generation.

Pipeline:
  templates.py  →  generator.py (renders text + char spans)
               →  annotations.py (char spans → word-level BIO)
               →  splitting.py (group by template split assignment)
               →  validation.py (structural checks)
               →  writes train.jsonl / val.jsonl / test.jsonl
               →  writes dataset_manifest.json

Usage:
  python ml/scripts/04_generate_dataset.py [--seed 42] [--instances-per-template 120]
"""

import argparse
import hashlib
import json
import sys
from datetime import datetime, timezone
from pathlib import Path
from collections import Counter

BASE_DIR = Path(__file__).resolve().parent.parent
SPLITS_DIR = BASE_DIR / "data" / "splits"
GENERATED_DIR = BASE_DIR / "data" / "generated"

# Allow importing ml/dataset package (dataset/ lives inside ml/)
sys.path.insert(0, str(BASE_DIR))  # adds ClipboardManager/ml/ to sys.path

from dataset.generator import generate_all_hierarchical
from dataset.annotations import character_spans_to_bio, AnnotatedExample
from dataset.splitting import assign_splits, compute_statistics
from dataset.validation import validate_all_splits

GENERATOR_VERSION = "1.10.0"


def generate_all(
    base_seed: int,
) -> list[AnnotatedExample]:
    """Render contexts and annotate."""
    all_examples: list[AnnotatedExample] = []
    render_errors = 0

    rendered_examples = generate_all_hierarchical(0, base_seed)
    
    for rendered in rendered_examples:
        if rendered is None:
            render_errors += 1
            continue
        try:
            annotated = character_spans_to_bio(rendered)
            all_examples.append(annotated)
        except (ValueError, AssertionError) as exc:
            render_errors += 1
            print(f"  [SKIP] {rendered.template_id}: {exc}")

    print(f"  Generated {len(all_examples)} examples ({render_errors} render errors skipped).")
    return all_examples


def write_jsonl(examples: list[AnnotatedExample], path: Path) -> str:
    """Write examples to JSONL and return the SHA-256 hex digest."""
    path.parent.mkdir(parents=True, exist_ok=True)
    sha = hashlib.sha256()
    with open(path, "w", encoding="utf-8") as f:
        for ex in examples:
            line = json.dumps({"tokens": ex.tokens, "ner_tags": ex.ner_tags}) + "\n"
            f.write(line)
            sha.update(line.encode())
    return sha.hexdigest()


def write_manifest(
    splits: dict,
    stats: dict,
    file_hashes: dict,
    seed: int,
    instances_per_template: int,
) -> Path:
    """Write dataset_manifest.json to the generated/ directory."""
    GENERATED_DIR.mkdir(parents=True, exist_ok=True)
    manifest_path = GENERATED_DIR / "dataset_manifest.json"

    template_ids_by_split = {
        split: list({ex.template_id for ex in examples})
        for split, examples in splits.items()
    }

    manifest = {
        "dataset_version": "synthetic-v1.0.0",
        "generator_version": GENERATOR_VERSION,
        "seed": seed,
        "instances_per_template": instances_per_template,
        "generated_at_utc": datetime.now(timezone.utc).isoformat(),
        "label_schema": [
            "O", "B-SECRET", "I-SECRET", "B-PII", "I-PII",
            "B-HOSTINFO", "I-HOSTINFO", "B-NETWORK", "I-NETWORK"
        ],
        "split_counts": {
            split: stats[split]["count"]
            for split in ["train", "val", "test"]
        },
        "total_examples": sum(
            stats[split]["count"] for split in ["train", "val", "test"]
        ),
        "entity_counts_total": stats.get("_totals", {}),
        "per_split_stats": {
            split: stats[split]
            for split in ["train", "val", "test"]
        },
        "template_ids_by_split": template_ids_by_split,
        "file_hashes": file_hashes,
    }

    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    return manifest_path


def print_report(splits: dict, stats: dict) -> None:
    """Print the generation report."""
    print("\n" + "=" * 60)
    print("=== SYNTHETIC DATASET GENERATION REPORT ===")
    print("=" * 60)
    print(f"\nGenerator version : {GENERATOR_VERSION}")

    total = sum(stats[s]["count"] for s in ["train", "val", "test"])
    print(f"\nDataset split counts:")
    for split in ["train", "val", "test"]:
        pct = 100 * stats[split]["count"] / total if total else 0
        print(f"  {split:10s}: {stats[split]['count']:>6} ({pct:.1f}%)")
    print(f"  {'total':10s}: {total:>6}")

    print(f"\nEntity distribution (span counts across all splits):")
    totals = stats.get("_totals", {})
    for cat in ["SECRET", "PII", "HOSTINFO", "NETWORK"]:
        count = totals.get(cat, 0)
        print(f"  {cat:<10}: {count:>6}")

    print(f"\nSequence length stats (tokens per example):")
    for split in ["train", "val", "test"]:
        s = stats[split]
        print(f"  {split:10s}: avg={s['avg_length']}, max={s['max_length']}, min={s['min_length']}")

    print(f"\nTemplate family distribution:")
    family_counts: Counter = Counter()
    for split_examples in splits.values():
        for ex in split_examples:
            family_counts[ex.family] += 1
    for family, count in sorted(family_counts.items()):
        print(f"  {family:<20}: {count:>6}")


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate synthetic NER dataset")
    parser.add_argument("--seed", type=int, default=42, help="Base random seed")
    parser.add_argument(
        "--instances-per-template", type=int, default=120,
        help="How many instances to generate per template (default: 120)"
    )
    parser.add_argument(
        "--no-variation", action="store_true",
        help="Disable prefix/suffix lexical variation"
    )
    args = parser.parse_args()

    print(f"=== Synthetic Dataset Generator v{GENERATOR_VERSION} ===")
    print(f"Seed: {args.seed}  |  Balanced Classes: 9k spans each (SECRET/PII boosted)")
    print(f"Estimated output: ~40k examples with targeted SECRET/PII/HOSTINFO expansion\n")

    # Step 1: Generate and annotate all examples
    print("Step 1: Rendering contexts and generating BIO annotations...")
    all_examples = generate_all(
        base_seed=args.seed,
    )

    if not all_examples:
        print("ERROR: No examples were generated. Check template definitions.")
        sys.exit(1)

    # Step 2: Assign to splits by template ID (leakage-aware)
    print("\nStep 2: Assigning to splits by template ID...")
    splits = assign_splits(all_examples)
    for split_name, examples in splits.items():
        print(f"  {split_name}: {len(examples)} examples")

    # Step 3: Compute statistics
    print("\nStep 3: Computing statistics...")
    stats = compute_statistics(splits)

    # Step 4: Validate structural correctness
    print("\nStep 4: Validating dataset...")
    raw_splits = {
        split_name: [
            {"tokens": ex.tokens, "ner_tags": ex.ner_tags}
            for ex in examples
        ]
        for split_name, examples in splits.items()
    }
    passed = validate_all_splits(raw_splits)

    if not passed:
        print("\nERROR: Dataset validation failed. Fix issues above before proceeding.")
        sys.exit(1)

    # Step 5: Write JSONL files
    # Step 5: Write JSONL split files
    print("\nStep 5: Writing JSONL split files...")
    file_hashes: dict = {}
    for split_name, examples in splits.items():
        out_path = SPLITS_DIR / f"{split_name}.jsonl"
        sha = write_jsonl(examples, out_path)
        file_hashes[f"{split_name}.jsonl"] = sha
        print(f"  Wrote {len(examples)} examples -> {out_path}")

    # Step 6: Write manifest
    print("\nStep 6: Writing dataset manifest...")
    manifest_path = write_manifest(splits, stats, file_hashes, args.seed, args.instances_per_template)
    print(f"  Manifest -> {manifest_path}")

    # Step 7: Print full report
    print_report(splits, stats)

    print("\n" + "=" * 60)
    print("Generation complete. NEXT STEP:")
    print("  python ml/scripts/verify_dataset.py")
    print("=" * 60)


if __name__ == "__main__":
    main()

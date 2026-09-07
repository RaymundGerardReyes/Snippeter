"""
ml/dataset/validation.py

Dataset structural and contract validation.

Validates:
  - JSON syntax (pre-checked before this module is called)
  - Required fields: tokens, ner_tags
  - tokens is a non-empty list of strings
  - len(tokens) == len(ner_tags)
  - All labels belong to the 9-label authoritative contract
  - BIO transition validity (I- must follow B- or I- of same type)
  - No exact token-sequence duplicates within a split
"""

from __future__ import annotations
from typing import List, Dict, Tuple

LABELS = ["O", "B-SECRET", "I-SECRET", "B-PII", "I-PII",
          "B-HOSTINFO", "I-HOSTINFO", "B-NETWORK", "I-NETWORK"]
LABEL_SET = set(LABELS)


class ValidationError(Exception):
    pass


def validate_record(record: dict, record_index: int, split_name: str) -> List[str]:
    """
    Validate a single JSONL record.
    Returns a list of error strings (empty list = OK).
    """
    errors: List[str] = []
    ctx = f"[{split_name}#{record_index}]"

    if "tokens" not in record:
        errors.append(f"{ctx} Missing field: 'tokens'")
    if "ner_tags" not in record:
        errors.append(f"{ctx} Missing field: 'ner_tags'")

    if errors:
        return errors

    tokens = record["tokens"]
    tags = record["ner_tags"]

    if not isinstance(tokens, list) or len(tokens) == 0:
        errors.append(f"{ctx} 'tokens' must be a non-empty list")
        return errors

    if not isinstance(tags, list):
        errors.append(f"{ctx} 'ner_tags' must be a list")
        return errors

    if len(tokens) != len(tags):
        errors.append(
            f"{ctx} Length mismatch: {len(tokens)} tokens vs {len(tags)} tags"
        )

    for i, tag in enumerate(tags):
        if tag not in LABEL_SET:
            errors.append(f"{ctx} Unknown label '{tag}' at position {i}")

    # BIO transition check
    errors.extend(_check_bio_transitions(tags, ctx))

    return errors


def _check_bio_transitions(tags: List[str], ctx: str) -> List[str]:
    """
    Verify BIO transitions:
      I-X must be preceded by B-X or I-X (same X).
    """
    errors: List[str] = []
    prev_tag = "O"
    for i, tag in enumerate(tags):
        if tag.startswith("I-"):
            entity_type = tag[2:]
            if not (prev_tag == f"B-{entity_type}" or prev_tag == f"I-{entity_type}"):
                errors.append(
                    f"{ctx} Invalid BIO transition at pos {i}: "
                    f"'{tag}' follows '{prev_tag}'"
                )
        prev_tag = tag
    return errors


def validate_split(
    records: List[dict],
    split_name: str,
) -> Tuple[List[str], int]:
    """
    Validate all records in a split.
    Returns (all_errors, duplicate_count).
    """
    all_errors: List[str] = []
    seen_texts: set[str] = set()
    dup_count = 0

    for idx, record in enumerate(records):
        errors = validate_record(record, idx, split_name)
        all_errors.extend(errors)

        key = " ".join(record.get("tokens", []))
        if key in seen_texts:
            dup_count += 1
        else:
            seen_texts.add(key)

    return all_errors, dup_count


def validate_all_splits(
    splits: Dict[str, List[dict]],
) -> bool:
    """
    Run full validation across train / val / test splits.
    Prints a report and returns True if all checks pass, False otherwise.
    """
    print("\n=== Dataset Validation ===")
    total_errors: List[str] = []
    total_dups = 0

    for split_name in ["train", "val", "test"]:
        records = splits.get(split_name, [])
        errors, dups = validate_split(records, split_name)
        total_errors.extend(errors)
        total_dups += dups
        status = "PASS" if not errors else f"FAIL ({len(errors)} errors)"
        print(f"  {split_name:10s}: {len(records):>6} records  |  BIO/label: {status}  |  internal dups: {dups}")

    print(f"\n  Total errors:     {len(total_errors)}")
    print(f"  Total duplicates: {total_dups}")

    if total_errors:
        print("\n  Errors:")
        for err in total_errors[:25]:
            print(f"    {err}")
        if len(total_errors) > 25:
            print(f"    ... and {len(total_errors) - 25} more")

    passed = len(total_errors) == 0
    print(f"\n  Result: {'PASS' if passed else 'FAIL'}")
    return passed

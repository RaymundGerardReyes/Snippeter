"""
ml/dataset/splitting.py

Leakage-aware split assignment.

Split strategy:
  - Templates are already pre-assigned to train / val / test in templates.py.
  - This module groups AnnotatedExamples by their template's split assignment.
  - Duplicate rendered texts are detected and removed across splits.
  - Summary statistics are reported per split.
"""

from __future__ import annotations
from collections import defaultdict, Counter
from typing import List, Dict, Tuple
from .annotations import AnnotatedExample


VALID_SPLITS = {"train", "val", "test"}
LABELS = ["O", "B-SECRET", "I-SECRET", "B-PII", "I-PII",
          "B-HOSTINFO", "I-HOSTINFO", "B-NETWORK", "I-NETWORK"]


def assign_splits(
    examples: List[AnnotatedExample],
) -> Dict[str, List[AnnotatedExample]]:
    """
    Separate examples by the split their originating template was assigned to.
    Also removes exact duplicate rendered-text across all splits.
    """
    buckets: Dict[str, List[AnnotatedExample]] = defaultdict(list)
    for ex in examples:
        split = _template_split(ex.template_id)
        buckets[split].append(ex)

    # Deduplicate: build a seen-text set and filter cross-split duplicates
    seen_texts: set[str] = set()
    clean: Dict[str, List[AnnotatedExample]] = {}
    duplicates_removed = 0

    for split in ["train", "val", "test"]:
        clean_split: List[AnnotatedExample] = []
        for ex in buckets.get(split, []):
            text_key = " ".join(ex.tokens)
            if text_key in seen_texts:
                duplicates_removed += 1
            else:
                seen_texts.add(text_key)
                clean_split.append(ex)
        clean[split] = clean_split

    print(f"  Deduplication removed {duplicates_removed} cross-split duplicates.")
    return clean


import hashlib

def _template_split(template_id: str) -> str:
    """
    Deterministically assign a split based on the template (context) ID.
    Guarantees that all variations of a single context end up in the same split,
    preventing leakage.
    Splits: ~70% train, 15% val, 15% test.
    """
    h = int(hashlib.md5(template_id.encode('utf-8')).hexdigest(), 16)
    r = h % 100
    if r < 15:
        return "test"
    elif r < 30:
        return "val"
    return "train"


def compute_statistics(splits: Dict[str, List[AnnotatedExample]]) -> Dict:
    """
    Return per-split and aggregate entity counts and sequence-length statistics.
    """
    stats = {}
    total_entity_counts: Counter = Counter()

    for split_name, examples in splits.items():
        entity_counts: Counter = Counter()
        lengths = []
        for ex in examples:
            lengths.append(len(ex.tokens))
            for tag in ex.ner_tags:
                if tag.startswith("B-"):
                    entity_counts[tag[2:]] += 1

        total_entity_counts.update(entity_counts)
        stats[split_name] = {
            "count": len(examples),
            "entity_counts": dict(entity_counts),
            "avg_length": round(sum(lengths) / len(lengths), 2) if lengths else 0,
            "max_length": max(lengths) if lengths else 0,
            "min_length": min(lengths) if lengths else 0,
        }

    stats["_totals"] = dict(total_entity_counts)
    return stats

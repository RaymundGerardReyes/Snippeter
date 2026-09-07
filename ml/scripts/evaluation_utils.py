"""
ml/scripts/evaluation_utils.py

Canonical unified evaluator for token classification.
Ensures that both 06_evaluate.py and analyze_predictions.py
use the exact same word-level decoding and span extraction logic.
"""

from collections import Counter
import numpy as np

def extract_spans(tokens: list[str], tags: list[str]) -> list[dict]:
    """Extract entity spans from word-level token/tag sequences with start/end token bounds."""
    assert len(tokens) == len(tags), f"Length mismatch: tokens={len(tokens)}, tags={len(tags)}"
    spans = []
    current = None
    for i, (tok, tag) in enumerate(zip(tokens, tags)):
        if tag.startswith("B-"):
            if current:
                spans.append(current)
            current = {"type": tag[2:], "tokens": [tok], "start": i, "end": i + 1}
        elif tag.startswith("I-") and current and current["type"] == tag[2:]:
            current["tokens"].append(tok)
            current["end"] = i + 1
        else:
            if current:
                spans.append(current)
                current = None
    if current:
        spans.append(current)
    for span in spans:
        span["text"] = " ".join(span["tokens"])
    return spans

def decode_word_level_predictions(logits, word_ids_batch, id2label):
    """
    Decodes subword logits back to word-level predictions.
    Takes the argmax for the first subword of each original word.
    
    Args:
        logits: numpy array of shape (batch_size, seq_len, num_labels)
        word_ids_batch: list of list of word_ids (from tokenizer.word_ids)
        id2label: dictionary mapping label IDs to string tags
        
    Returns:
        word_preds_batch: list of list of predicted tags (word level)
    """
    preds = np.argmax(logits, axis=2)
    word_preds_batch = []
    
    for pred_seq, word_ids in zip(preds, word_ids_batch):
        word_preds = []
        previous_word_id = None
        for pred, word_id in zip(pred_seq, word_ids):
            if word_id is None:
                continue
            if word_id != previous_word_id:
                word_preds.append(id2label[pred])
            previous_word_id = word_id
        word_preds_batch.append(word_preds)
        
    return word_preds_batch

def evaluate_predictions(records, all_pred_tags, all_true_tags, entity_types):
    """
    Runs seqeval and detailed manual span tracking (TP, FP, FN).
    Ensures length invariants.
    """
    # 1. Verify invariants
    num_truncated = 0
    for i, (record, pred_tags, true_tags) in enumerate(zip(records, all_pred_tags, all_true_tags)):
        # Because we decoded based on word_ids, length is exactly the number of encoded words.
        # It could be less than len(record["tokens"]) if truncation occurred.
        assert len(pred_tags) == len(true_tags), f"Pred/True length mismatch at record {i}"
        assert len(pred_tags) <= len(record["tokens"]), f"Pred exceeds tokens at record {i}"
        if len(pred_tags) < len(record["tokens"]):
            num_truncated += 1
            
    # 2. (seqeval removed to prevent MemoryError)
    
    # 3. Manual Error Categorization (including Boundary Mismatches)
    false_positives = []
    false_negatives = []
    wrong_category = []
    boundary_mismatches = []
    total_true_spans = 0
    total_pred_spans = 0
    span_type_confusion = Counter()
    
    # Track per-category TP/FP/FN
    cat_counts = {cat: {"TP": 0, "FP": 0, "FN": 0} for cat in entity_types}
    
    for rec_idx, (pred_tags, true_tags, record) in enumerate(zip(all_pred_tags, all_true_tags, records)):
        tokens = record["tokens"][:len(pred_tags)]
        
        pred_spans = extract_spans(tokens, pred_tags)
        true_spans = extract_spans(tokens, true_tags)
        
        total_true_spans += len(true_spans)
        total_pred_spans += len(pred_spans)
        
        # Track matched true spans to find FN later
        matched_true = set()
        
        for ps in pred_spans:
            # Check for exact match (start, end, type)
            exact_match = any(ts["start"] == ps["start"] and ts["end"] == ps["end"] and ts["type"] == ps["type"] for ts in true_spans)
            
            if exact_match:
                cat_counts[ps["type"]]["TP"] += 1
                for idx_ts, ts in enumerate(true_spans):
                    if ts["start"] == ps["start"] and ts["end"] == ps["end"] and ts["type"] == ps["type"]:
                        matched_true.add(idx_ts)
            else:
                cat_counts[ps["type"]]["FP"] += 1
                # Check if there is an overlapping true span
                overlapping = [ts for idx_ts, ts in enumerate(true_spans) if max(ps["start"], ts["start"]) < min(ps["end"], ts["end"])]
                
                if overlapping:
                    ts_match = overlapping[0]
                    matched_true.add(true_spans.index(ts_match))
                    if ts_match["type"] == ps["type"]:
                        # Same entity type, wrong boundary!
                        if ps["start"] > ts_match["start"] or ps["end"] < ts_match["end"]:
                            mismatch_type = "Truncated"
                        elif ps["start"] < ts_match["start"] or ps["end"] > ts_match["end"]:
                            mismatch_type = "Over-extended"
                        else:
                            mismatch_type = "Offset"

                        boundary_mismatches.append({
                            "record": rec_idx,
                            "tokens": tokens,
                            "type": ps["type"],
                            "pred_text": ps["text"],
                            "true_text": ts_match["text"],
                            "pred_bounds": (ps["start"], ps["end"]),
                            "true_bounds": (ts_match["start"], ts_match["end"]),
                            "mismatch_type": mismatch_type
                        })
                    else:
                        # Overlapping span with wrong entity category
                        span_type_confusion[(ts_match["type"], ps["type"])] += 1
                        wrong_category.append({
                            "record": rec_idx,
                            "tokens": tokens,
                            "predicted": ps["type"],
                            "actual": ts_match["type"],
                            "pred_text": ps["text"],
                            "true_text": ts_match["text"],
                        })
                else:
                    # No overlap at all: pure False Positive
                    false_positives.append({
                        "record": rec_idx,
                        "tokens": tokens,
                        "predicted": ps["type"],
                        "text": ps["text"],
                    })
                    
        for idx_ts, ts in enumerate(true_spans):
            if idx_ts not in matched_true:
                cat_counts[ts["type"]]["FN"] += 1
                false_negatives.append({
                    "record": rec_idx,
                    "tokens": tokens,
                    "actual": ts["type"],
                    "text": ts["text"],
                })

    # Manual F1 sanity check
    overall_tp = sum(c["TP"] for c in cat_counts.values())
    overall_fp = sum(c["FP"] for c in cat_counts.values())
    overall_fn = sum(c["FN"] for c in cat_counts.values())
    
    manual_precision = overall_tp / (overall_tp + overall_fp) if (overall_tp + overall_fp) > 0 else 0
    manual_recall = overall_tp / (overall_tp + overall_fn) if (overall_tp + overall_fn) > 0 else 0
    manual_f1 = 2 * manual_precision * manual_recall / (manual_precision + manual_recall) if (manual_precision + manual_recall) > 0 else 0

    analysis = {
        "overall_metrics": {
            "f1": manual_f1,
            "precision": manual_precision,
            "recall": manual_recall,
            "accuracy": 0.0,
            "manual_f1": manual_f1,
            "manual_precision": manual_precision,
            "manual_recall": manual_recall
        },
        "per_category": {
            cat: {
                "f1": 2 * (cat_counts[cat]["TP"] / (cat_counts[cat]["TP"] + cat_counts[cat]["FP"]) * cat_counts[cat]["TP"] / (cat_counts[cat]["TP"] + cat_counts[cat]["FN"])) / ((cat_counts[cat]["TP"] / (cat_counts[cat]["TP"] + cat_counts[cat]["FP"])) + (cat_counts[cat]["TP"] / (cat_counts[cat]["TP"] + cat_counts[cat]["FN"]))) if cat_counts[cat]["TP"] > 0 else 0,
                "precision": cat_counts[cat]["TP"] / (cat_counts[cat]["TP"] + cat_counts[cat]["FP"]) if (cat_counts[cat]["TP"] + cat_counts[cat]["FP"]) > 0 else 0,
                "recall": cat_counts[cat]["TP"] / (cat_counts[cat]["TP"] + cat_counts[cat]["FN"]) if (cat_counts[cat]["TP"] + cat_counts[cat]["FN"]) > 0 else 0,
                "TP": cat_counts[cat]["TP"],
                "FP": cat_counts[cat]["FP"],
                "FN": cat_counts[cat]["FN"]
            }
            for cat in entity_types
        },
        "error_summary": {
            "total_true_spans": total_true_spans,
            "total_pred_spans": total_pred_spans,
            "false_positives": len(false_positives),
            "false_negatives": len(false_negatives),
            "wrong_category": len(wrong_category),
            "boundary_mismatches": len(boundary_mismatches),
            "num_truncated_records": num_truncated
        },
        "span_type_confusion": {
            f"{t} -> {p}": c for (t, p), c in span_type_confusion.most_common()
        },
        "boundary_mismatch_examples": boundary_mismatches,
        "false_positive_examples": false_positives,
        "false_negative_examples": false_negatives,
        "wrong_category_examples": wrong_category,
    }
    
    return analysis

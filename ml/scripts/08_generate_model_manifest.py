"""
ml/scripts/08_generate_model_manifest.py

Generates and verifies the version manifest for an exported ONNX model.
Enforces SHA-256 content hashing, SemVer metadata, and minimum recall gates.

Usage:
    python 08_generate_model_manifest.py --version 1.0.0 --onnx-path ml/models/onnx/secret_pii_detector.onnx --dataset-snapshot dataset-2026.08.31-v1 --training-config-hash sha256:dummy
"""
import argparse
import hashlib
import json
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

SUPPORTED_LABEL_SCHEMA_VERSION = 1
LABELS = ["O", "B-SECRET", "I-SECRET", "B-PII", "I-PII",
          "B-HOSTINFO", "I-HOSTINFO", "B-NETWORK", "I-NETWORK"]

def sha256_of_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()

def git_commit_short() -> str:
    try:
        return subprocess.check_output(
            ["git", "rev-parse", "--short", "HEAD"],
            text=True,
            stderr=subprocess.DEVNULL,  # suppress "not a git repository" noise
        ).strip()
    except Exception:
        return None  # explicitly null — not fabricated

def load_eval_metrics(eval_json_path: Path) -> dict:
    # FAIL CLOSED: do not allow placeholder/hardcoded metrics in the manifest.
    # If evaluation.json is absent, the manifest MUST NOT be generated.
    if not eval_json_path.exists():
        sys.exit(
            f"REJECTED: evaluation.json not found at {eval_json_path}.\n"
            f"Run Stage 06 (06_evaluate.py) to produce real metrics before generating a manifest."
        )
    data = json.loads(eval_json_path.read_text(encoding="utf-8"))
    # Verify it contains real fields — not placeholder defaults
    if data.get("span_recall_overall") is None or data.get("span_f1_overall") is None:
        sys.exit(
            "REJECTED: evaluation.json is missing required metric keys.\n"
            "Ensure Stage 06 completed successfully before running Stage 08."
        )
    return data

def validate_recall_gate(eval_metrics: dict, min_recall: float = 0.85) -> None:
    recall = eval_metrics.get("span_recall_overall")
    if recall is None:
        raise ValueError("evaluation.json missing 'span_recall_overall' — cannot gate release.")
    if recall < min_recall:
        raise SystemExit(
            f"REJECTED: span_recall_overall={recall:.3f} is below the required "
            f"minimum {min_recall:.3f}. This model version will NOT be manifested."
        )

def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--version", required=True, help="SemVer, e.g. 1.0.0")
    parser.add_argument("--onnx-path", required=True, type=Path)
    parser.add_argument("--eval-path", type=Path, default=Path("ml/models/onnx/evaluation.json"))
    parser.add_argument("--dataset-snapshot", required=True, help="e.g. dataset-2026.08.31-v1")
    parser.add_argument("--training-config-hash", required=True)
    parser.add_argument("--min-app-major-version", type=int, default=1)
    parser.add_argument("--min-recall", type=float, default=0.85)
    parser.add_argument("--out", type=Path, default=None)
    args = parser.parse_args()

    if not args.onnx_path.exists():
        sys.exit(f"ONNX file not found: {args.onnx_path}")

    eval_metrics = load_eval_metrics(args.eval_path)
    validate_recall_gate(eval_metrics, args.min_recall)

    manifest = {
        "model_name": "secret_pii_detector",
        "semver": args.version,
        "content_sha256": sha256_of_file(args.onnx_path),
        "onnx_ir_version": 8,
        "opset_version": 17,
        "label_schema_version": SUPPORTED_LABEL_SCHEMA_VERSION,
        "labels": LABELS,
        "tokenizer": {
            "type": "wordpiece",
            "vocab_file": "vocab.txt",
            "max_sequence_length": 256,
        },
        "dataset_snapshot": args.dataset_snapshot,
        "training_config_hash": args.training_config_hash,
        "git_commit": git_commit_short(),
        "exported_at_utc": datetime.now(timezone.utc).isoformat(),
        "evaluation": eval_metrics,
        "min_app_major_version": args.min_app_major_version,
    }

    out_path = args.out or (args.onnx_path.parent / "model.manifest.json")
    out_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")

    print(f"Manifest successfully generated and written to {out_path}")
    print(f"  semver={manifest['semver']}  sha256={manifest['content_sha256'][:12]}...")
    print(f"  recall={eval_metrics.get('span_recall_overall'):.3f}  f1={eval_metrics.get('span_f1_overall'):.3f}")

if __name__ == "__main__":
    main()

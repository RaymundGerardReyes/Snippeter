import os
import json
import hashlib
from pathlib import Path

BASE_DIR = Path(__file__).resolve().parent.parent
ROOT_DIR = BASE_DIR.parent
TARGET_VERSIONS_DIR = ROOT_DIR / "ClipboardManager" / "Models" / "ml" / "versions" / "1.0.0"
POINTER_FILE = ROOT_DIR / "ClipboardManager" / "Models" / "ml" / "current.json"

def compute_sha256(file_path: Path) -> str:
    sha256 = hashlib.sha256()
    with open(file_path, "rb") as f:
        for chunk in iter(lambda: f.read(4096), b""):
            sha256.update(chunk)
    return sha256.hexdigest()

def main():
    TARGET_VERSIONS_DIR.mkdir(parents=True, exist_ok=True)
    onnx_file = TARGET_VERSIONS_DIR / "secret_pii_detector.onnx"
    manifest_file = TARGET_VERSIONS_DIR / "model.manifest.json"

    # Create dummy/model binary if missing
    if not onnx_file.exists():
        with open(onnx_file, "wb") as f:
            f.write(b"ONNX_DUMMY_MODEL_BINARY_PLUMBING_V1")
        print(f"Created ONNX binary placeholder: {onnx_file.name}")

    sha256_hash = compute_sha256(onnx_file)

    manifest_data = {
        "semver": "1.0.0",
        "name": "Secret & PII Token Classifier",
        "description": "DistilBERT-base fine-tuned on Docker logs and public PII datasets for DirectML GPU inference",
        "content_sha256": sha256_hash,
        "labels": ["O", "B-SECRET", "I-SECRET", "B-PII", "I-PII", "B-HOSTINFO", "I-HOSTINFO", "B-NETWORK", "I-NETWORK"],
        "max_seq_length": 128
    }

    with open(manifest_file, "w", encoding="utf-8") as f:
        json.dump(manifest_data, f, indent=2)
    print(f"Generated manifest: {manifest_file.name}")

    with open(POINTER_FILE, "w", encoding="utf-8") as f:
        json.dump({"active_version": "1.0.0"}, f, indent=2)
    print(f"Updated pointer: {POINTER_FILE.name}")

if __name__ == "__main__":
    main()

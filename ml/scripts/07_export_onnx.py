import json
import hashlib
import subprocess
from pathlib import Path
import torch
import onnxruntime as ort
import numpy as np
from transformers import DistilBertForTokenClassification, DistilBertTokenizerFast

BASE_DIR = Path(__file__).resolve().parent.parent
ROOT_DIR = BASE_DIR.parent
MODEL_DIR = BASE_DIR / "models" / "checkpoints" / "final_model"
TARGET_VERSIONS_DIR = ROOT_DIR / "Models" / "ml" / "versions" / "1.0.0"


def get_git_commit() -> str:
    try:
        return subprocess.check_output(["git", "rev-parse", "HEAD"], text=True).strip()
    except Exception:
        return "unknown"


def main():
    TARGET_VERSIONS_DIR.mkdir(parents=True, exist_ok=True)
    onnx_file = TARGET_VERSIONS_DIR / "secret_pii_detector.onnx"
    
    print("Loading PyTorch model and tokenizer...")
    if not MODEL_DIR.exists():
        raise FileNotFoundError(f"Model directory not found: {MODEL_DIR}. Run training script first.")

    model = DistilBertForTokenClassification.from_pretrained(str(MODEL_DIR))
    model.eval()

    tokenizer = DistilBertTokenizerFast.from_pretrained(str(MODEL_DIR))
    tokenizer.save_pretrained(str(TARGET_VERSIONS_DIR))

    # Dummy input for graph export
    dummy_input_ids = torch.randint(1, 1000, (1, 128), dtype=torch.long)
    dummy_attention_mask = torch.ones(1, 128, dtype=torch.long)

    print("Exporting ONNX graph with dynamic axes...")
    torch.onnx.export(
        model, 
        (dummy_input_ids, dummy_attention_mask), 
        str(onnx_file),
        input_names=['input_ids', 'attention_mask'], 
        output_names=['output'],
        dynamic_axes={
            'input_ids': {0: 'batch_size', 1: 'sequence_length'},
            'attention_mask': {0: 'batch_size', 1: 'sequence_length'},
            'output': {0: 'batch_size', 1: 'sequence_length'}
        },
        opset_version=14,
        do_constant_folding=True
    )
    
    # Calculate ONNX SHA256
    with open(onnx_file, "rb") as f:
        onnx_sha256 = hashlib.sha256(f.read()).hexdigest()

    print("\nValidating ONNX Artifact via ONNX Runtime & PyTorch Parity Matrix...")
    session = ort.InferenceSession(str(onnx_file), providers=["CPUExecutionProvider"])
    
    # Validate contract
    input_names = [i.name for i in session.get_inputs()]
    output_names = [o.name for o in session.get_outputs()]
    assert "input_ids" in input_names, "Missing input_ids in ONNX graph!"
    assert "attention_mask" in input_names, "Missing attention_mask in ONNX graph!"
    assert "output" in output_names, "Missing output in ONNX graph!"

    print(f"  Target Input Names : {input_names} [OK]")
    print(f"  Target Output Names: {output_names} [OK]")

    # Dynamic Shapes & Parity Test Matrix
    batch_sizes = [1, 2, 4, 8, 16]
    seq_lengths = [16, 32, 64, 128, 256]
    tested_shapes = []

    print("\n=== Running Dynamic Shape & Parity Check Matrix ===")
    for b in batch_sizes:
        for seq_len in seq_lengths:
            # Generate random test inputs
            test_ids = torch.randint(1, 25000, (b, seq_len), dtype=torch.long)
            test_mask = torch.ones((b, seq_len), dtype=torch.long)
            
            # PyTorch forward pass
            with torch.no_grad():
                pt_out = model(input_ids=test_ids, attention_mask=test_mask)
                pt_logits = pt_out.logits.cpu().numpy()
                pt_preds = np.argmax(pt_logits, axis=-1)

            # ONNX Runtime forward pass
            ort_inputs = {
                "input_ids": test_ids.numpy(),
                "attention_mask": test_mask.numpy()
            }
            ort_out = session.run(["output"], ort_inputs)
            onnx_logits = ort_out[0]
            onnx_preds = np.argmax(onnx_logits, axis=-1)

            # Verification assertions
            assert onnx_logits.shape == (b, seq_len, 9), (
                f"Shape mismatch for B={b}, L={seq_len}: got {onnx_logits.shape}"
            )
            np.testing.assert_allclose(
                pt_logits, onnx_logits, rtol=1e-3, atol=1e-3,
                err_msg=f"Numerical discrepancy for B={b}, L={seq_len}"
            )
            np.testing.assert_array_equal(
                pt_preds, onnx_preds,
                err_msg=f"Classification mismatch for B={b}, L={seq_len}"
            )
            tested_shapes.append(f"({b}, {seq_len})")

    print(f"Parity Check Matrix: PASS ({len(tested_shapes)} dynamic shapes verified)")

    # Read dataset_sha256 from training summary if available
    summary_file = MODEL_DIR / "training_summary.json"
    dataset_sha256 = "unknown"
    if summary_file.exists():
        with open(summary_file, "r", encoding="utf-8") as f:
            summary_data = json.load(f)
            dataset_sha256 = summary_data.get("dataset_sha256", "unknown")

    # Generate Release Manifest
    manifest_data = {
        "model_name": "secret_pii_detector",
        "version": "1.0.0",
        "onnx_file": "secret_pii_detector.onnx",
        "onnx_sha256": onnx_sha256,
        "git_commit": get_git_commit(),
        "dataset_sha256": dataset_sha256,
        "num_labels": 9,
        "opset_version": 14,
        "dynamic_shapes_tested": tested_shapes,
        "parity_check": "PASS",
        "tokenizer_config": "tokenizer_config.json"
    }

    manifest_path = TARGET_VERSIONS_DIR / "onnx_manifest.json"
    with open(manifest_path, "w", encoding="utf-8") as f:
        json.dump(manifest_data, f, indent=2)

    print(f"\nWrote Release Manifest -> {manifest_path}")
    print("\nSUCCESS: Real ONNX graph exported and validated against C# contract.")


if __name__ == "__main__":
    main()

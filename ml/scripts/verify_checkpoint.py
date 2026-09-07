import json
from pathlib import Path
import torch
from transformers import DistilBertTokenizerFast, DistilBertForTokenClassification

BASE_DIR = Path(__file__).resolve().parent.parent
FINAL_MODEL_DIR = BASE_DIR / "models" / "checkpoints" / "final_model"
TEST_FILE = BASE_DIR / "data" / "splits" / "test.jsonl"

def main():
    print(f"Testing reload from {FINAL_MODEL_DIR}...")
    if not FINAL_MODEL_DIR.exists():
        print("FAIL: Final model directory does not exist.")
        return False
        
    tokenizer = DistilBertTokenizerFast.from_pretrained(str(FINAL_MODEL_DIR))
    model = DistilBertForTokenClassification.from_pretrained(str(FINAL_MODEL_DIR))
    model.eval()
    
    print(f"Model num_labels: {model.config.num_labels}")
    assert model.config.num_labels == 9, f"Expected 9 labels, got {model.config.num_labels}"
    
    # Load first sample from test set
    if not TEST_FILE.exists():
        print(f"Warning: {TEST_FILE} not found. Using raw sample string.")
        sample_words = ["Connecting", "to", "postgres://admin:pass@db:5432/prod"]
    else:
        with open(TEST_FILE, "r", encoding="utf-8") as f:
            first_line = json.loads(f.readline())
            sample_words = first_line["tokens"]
            
    inputs = tokenizer(sample_words, is_split_into_words=True, return_tensors="pt")
    
    with torch.no_grad():
        outputs = model(**inputs)
        
    logits = outputs.logits
    batch_size, seq_len, num_labels = logits.shape
    print(f"Logits shape: [{batch_size}, {seq_len}, {num_labels}]")
    
    assert batch_size == 1, "Expected batch_size == 1"
    assert num_labels == 9, f"Expected num_labels == 9, got {num_labels}"
    
    print("\nStage 05 Checkpoint Reload & Inference: PASS")
    return True

if __name__ == "__main__":
    main()

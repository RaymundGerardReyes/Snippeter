import json
from pathlib import Path

BASE_DIR = Path(__file__).resolve().parent.parent
EVAL_OUT = BASE_DIR / "data" / "challenge" / "challenge_evaluation.json"

def main():
    with open(EVAL_OUT) as f:
        analysis = json.load(f)
        
    print("--- Boundary Audit ---")
    fp_list = analysis["false_positive_examples"]
    fn_list = analysis["false_negative_examples"]
    
    # Simple matching of similar strings in FP and FN
    for fp in fp_list:
        for fn in fn_list:
            if fp["record"] == fn["record"] and fp["predicted"] == fn["actual"]:
                # They are in the same record and same category, likely a boundary mismatch
                print(f"Record {fp['record']}:")
                print(f"  Tokens: {' '.join(fp['tokens'])}")
                print(f"  Reference (FN): '{fn['text']}'")
                print(f"  Predicted (FP): '{fp['text']}'")
                print()

if __name__ == "__main__":
    main()

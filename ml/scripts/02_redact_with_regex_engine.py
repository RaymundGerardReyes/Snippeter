import os
import re
import json
from pathlib import Path

BASE_DIR = Path(__file__).resolve().parent.parent
INPUT_DIR = BASE_DIR / "data" / "raw_docker_logs"
OUTPUT_DIR = BASE_DIR / "data" / "redacted_docker_logs"

# Regex patterns matching Phase 2 engine detectors
PATTERNS = [
    (re.compile(r"(?i)\b(?:postgres|mysql|mongodb|redis)://[^\s""']+"), "[REDACTED-DB-URL]"),
    (re.compile(r"(?i)(?:https?|ftp)://[^\s""']+"), "[REDACTED-URL]"),
    (re.compile(r"(?i)\b(?:password|pwd|secret|token|key)\s*[:=]\s*([^\s""';]+)"), "[REDACTED-CRED]"),
    (re.compile(r"\b(?:\d{1,3}\.){3}\d{1,3}\b"), "[REDACTED-IP]"),
    (re.compile(r"\b[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}\b"), "[REDACTED-EMAIL]"),
    (re.compile(r"\b(?:[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}|[0-9a-fA-F]{32,64})\b"), "[REDACTED-HASH]")
]

def redact_message(message: str):
    redacted = message
    findings = []
    
    for pattern, placeholder in PATTERNS:
        for match in pattern.finditer(message):
            findings.append({
                "start": match.start(),
                "end": match.end(),
                "matched_text": match.group(0),
                "placeholder": placeholder
            })
            redacted = pattern.sub(placeholder, redacted)
            
    return redacted, findings

def main():
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    
    jsonl_files = list(INPUT_DIR.glob("*.jsonl"))
    if not jsonl_files:
        print("No raw log files found. Creating dummy redacted log sample...")
        dummy_sample = OUTPUT_DIR / "sample_redacted.jsonl"
        with open(dummy_sample, "w", encoding="utf-8") as f:
            f.write(json.dumps({
                "original": "User admin logged in from 192.168.1.50 with password: MySecretPass123",
                "redacted": "User admin logged in from [REDACTED-IP] with [REDACTED-CRED]",
                "findings": 2
            }) + "\n")
        print(f"Sample written to {dummy_sample.name}")
        return

    for log_path in jsonl_files:
        out_path = OUTPUT_DIR / f"{log_path.stem}_redacted.jsonl"
        count = 0
        with open(log_path, "r", encoding="utf-8") as infile, open(out_path, "w", encoding="utf-8") as outfile:
            for line in infile:
                try:
                    record = json.loads(line.strip())
                    payload = record.get("message_payload", "")
                    redacted, findings = redact_message(payload)
                    record["redacted_payload"] = redacted
                    record["regex_findings"] = findings
                    outfile.write(json.dumps(record) + "\n")
                    count += 1
                except json.JSONDecodeError:
                    continue
        print(f"Processed {count} lines into {out_path.name}")

if __name__ == "__main__":
    main()

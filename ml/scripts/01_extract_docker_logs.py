import os
import re
import json
from pathlib import Path

# Paths relative to the script location
BASE_DIR = Path(__file__).resolve().parent.parent
RAW_DIR = BASE_DIR / "data" / "raw_docker_logs"
OUTPUT_DIR = BASE_DIR / "data" / "redacted_docker_logs"

# Matches standard Docker/ASP.NET logs: "2023-10-25T12:00:00.000Z [Information] Category: Message"
# Adjust this regex if your specific logging framework uses a different shape.
LOG_PATTERN = re.compile(
    r"^(?P<timestamp>\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})?)\s+"
    r"\[(?P<level>\w+)\]\s+"
    r"(?P<category>[^:]+):\s+"
    r"(?P<message>.*)$"
)

def process_logs():
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    
    for log_file in RAW_DIR.glob("*.log"):
        print(f"Processing {log_file.name}...")
        parsed_lines = []
        
        with open(log_file, "r", encoding="utf-8") as f:
            for line in f:
                match = LOG_PATTERN.match(line.strip())
                if match:
                    # We only care about isolating the message for PII/Secret detection
                    parsed_lines.append({
                        "original_line": line.strip(),
                        "message_payload": match.group("message")
                    })
                else:
                    # Fallback for unformatted lines or multi-line stack traces
                    parsed_lines.append({
                        "original_line": line.strip(),
                        "message_payload": line.strip()
                    })
        
        output_path = OUTPUT_DIR / f"{log_file.stem}_extracted.jsonl"
        with open(output_path, "w", encoding="utf-8") as out_f:
            for item in parsed_lines:
                out_f.write(json.dumps(item) + "\n")
                
        print(f"Extracted {len(parsed_lines)} lines to {output_path.name}")

if __name__ == "__main__":
    process_logs()
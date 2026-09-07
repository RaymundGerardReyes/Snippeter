import json
import re
from pathlib import Path

BASE_DIR = Path(__file__).resolve().parent.parent
CHALLENGE_DIR = BASE_DIR / "data" / "challenge"

TAG_RE = re.compile(r'<(SECRET|PII|HOSTINFO|NETWORK)>(.*?)</\1>')

CHALLENGE_TEXTS = [
    # API Authorization and Secrets (varied formats)
    "Authorization: Bearer <SECRET>eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c</SECRET>",
    "X-API-Key: <SECRET>ak_live_xyz1234567890abcdef</SECRET>",
    "const dbPassword = process.env.DB_PASS || '<SECRET>s#p3rS3cr3t!</SECRET>';",
    "curl -H 'Authorization: Token <SECRET>ghp_9876543210abcdefGHIJKLMNOPQRSTUVWXYZ</SECRET>'",
    "{\"auth\": {\"type\": \"basic\", \"credentials\": \"<SECRET>dXNlcjpwYXNz</SECRET>\"}}",
    
    # Natural-Language PII
    "I spoke with <PII>Dr. Gregory House</PII> regarding the incident.",
    "Please send the invoice to <PII>Sarah Connor</PII> at <PII>123 Sky Net Blvd, LA</PII>.",
    "Her phone number is <PII>(555) 867-5309</PII>, call her tomorrow.",
    "Contact <PII>admin@secure-corp.com</PII> for your password reset.",
    "Hey <PII>Charlie</PII>, did you see the email from <PII>Alice</PII>?",
    
    # Ports in Code
    "app.listen(<NETWORK>8080</NETWORK>, () => console.log('started'));",
    "config.set('server.port', <NETWORK>3000</NETWORK>);",
    "export const PORT = process.env.PORT || <NETWORK>8443</NETWORK>;",
    "new Server({ port: <NETWORK>5432</NETWORK>, host: '<HOSTINFO>0.0.0.0</HOSTINFO>' });",
    
    # Hard Negatives
    "The process exited with code 500 after 424242 ms.",
    "Enable 2FA by scanning this QR code.",
    "Session UUID: 123e4567-e89b-12d3-a456-426614174000.",
    "Visit docs.example.com for more info.",
    "We need to test against the mock endpoint at api.example.org.",
    "User ID 424242 requested a password reset.",
    
    # Composite Sockets
    "Failed to connect to <HOSTINFO>redis.internal</HOSTINFO>:<NETWORK>6379</NETWORK>.",
    "ProxyPass /api http://<HOSTINFO>10.1.2.3</HOSTINFO>:<NETWORK>8000</NETWORK>/",
    "tcp://<HOSTINFO>192.168.100.5</HOSTINFO>:<NETWORK>5672</NETWORK>",
    "Database host is <HOSTINFO>db-primary.corp.local</HOSTINFO>:<NETWORK>3306</NETWORK>.",
    
    # Ambiguous IPv6 and IPv4
    "The loopback address is <HOSTINFO>::1</HOSTINFO> or <HOSTINFO>127.0.0.1</HOSTINFO>.",
    "Client IP <HOSTINFO>2001:db8::ff00:42:8329</HOSTINFO> was blocked.",
    "Allowed origin: https://<HOSTINFO>[fe80::1]</HOSTINFO>:<NETWORK>443</NETWORK>",
    "Host <HOSTINFO>10.0.2.15</HOSTINFO> mapped to <HOSTINFO>192.168.1.10</HOSTINFO>.",
    
    # MAC Addresses
    "Device <NETWORK>02:42:ac:11:00:02</NETWORK> joined the network.",
    "MAC address <NETWORK>00:1A:2B:3C:4D:5E</NETWORK> is banned.",
    "ARP reply for <HOSTINFO>192.168.1.1</HOSTINFO> is <NETWORK>ff:ff:ff:ff:ff:ff</NETWORK>.",
    
    # More Edge Cases
    "mysql://<PII>root</PII>:<SECRET>r00tp@ss</SECRET>@<HOSTINFO>localhost</HOSTINFO>:<NETWORK>3306</NETWORK>/db",
    "mongodb+srv://<PII>admin</PII>:<SECRET>admin123</SECRET>@<HOSTINFO>cluster0.mongodb.net</HOSTINFO>/test",
    "ssh <PII>user</PII>@<HOSTINFO>192.168.1.5</HOSTINFO> -p <NETWORK>2222</NETWORK>"
]

def tokenize_and_tag(text: str):
    tokens = []
    tags = []
    
    clean_text = ""
    spans = [] 
    
    cursor = 0
    remaining = text
    while remaining:
        match = TAG_RE.search(remaining)
        if not match:
            clean_text += remaining
            break
            
        before = remaining[:match.start()]
        clean_text += before
        cursor += len(before)
        
        label = match.group(1)
        entity_text = match.group(2)
        
        spans.append((cursor, cursor + len(entity_text), label))
        clean_text += entity_text
        cursor += len(entity_text)
        
        remaining = remaining[match.end():]
        
    # Split into words and punctuation
    parts = []
    for match in re.finditer(r'\w+|[^\w\s]|\s+', clean_text):
        parts.append(match.group())
    
    current_char = 0
    for part in parts:
        if not part:
            continue
        
        is_whitespace = part.isspace()
        start_char = current_char
        end_char = current_char + len(part)
        
        if not is_whitespace:
            tokens.append(part)
            part_tag = "O"
            for span_start, span_end, label in spans:
                if start_char >= span_start and end_char <= span_end:
                    if start_char == span_start:
                        part_tag = f"B-{label}"
                    else:
                        part_tag = f"I-{label}"
                    break
                elif start_char < span_end and end_char > span_start:
                    if start_char == span_start:
                        part_tag = f"B-{label}"
                    else:
                        part_tag = f"I-{label}"
                    break
            tags.append(part_tag)
            
        current_char = end_char
        
    return {"tokens": tokens, "ner_tags": tags}

def main():
    CHALLENGE_DIR.mkdir(parents=True, exist_ok=True)
    out_path = CHALLENGE_DIR / "challenge-v2.jsonl"
    
    examples = []
    for text in CHALLENGE_TEXTS:
        ex = tokenize_and_tag(text)
        examples.append(ex)
        
    with open(out_path, "w", encoding="utf-8") as f:
        for ex in examples:
            f.write(json.dumps(ex) + "\n")
            
    print(f"Created challenge set V2 with {len(examples)} examples at {out_path}")

if __name__ == "__main__":
    main()

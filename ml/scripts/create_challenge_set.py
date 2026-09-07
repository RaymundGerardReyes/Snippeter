import json
import re
from pathlib import Path

BASE_DIR = Path(__file__).resolve().parent.parent
CHALLENGE_DIR = BASE_DIR / "data" / "challenge"

TAG_RE = re.compile(r'<(SECRET|PII|HOSTINFO|NETWORK)>(.*?)</\1>')

CHALLENGE_TEXTS = [
    # --- 1. Bare Ports in Code Contexts ---
    "port = <NETWORK>27017</NETWORK>",
    "serverPort: <NETWORK>8080</NETWORK>",
    "const PORT = <NETWORK>443</NETWORK>;",
    "export PORT=<NETWORK>5000</NETWORK>",
    ".listen(<NETWORK>3000</NETWORK>)",
    "app.listen(port=<NETWORK>80</NETWORK>, host='<HOSTINFO>0.0.0.0</HOSTINFO>')",
    "connect(host='<HOSTINFO>localhost</HOSTINFO>', port=<NETWORK>5432</NETWORK>)",
    "\"port\": <NETWORK>6379</NETWORK>,",
    "server { listen <NETWORK>443</NETWORK> ssl; }",
    "bind <HOSTINFO>127.0.0.1</HOSTINFO> <NETWORK>6379</NETWORK>",
    "redis.port = <NETWORK>6379</NETWORK>",
    "DB_PORT=<NETWORK>3306</NETWORK>",
    "config.set('port', <NETWORK>9200</NETWORK>)",
    "{ \"targetPort\": <NETWORK>8443</NETWORK> }",
    "EXPOSE <NETWORK>8080</NETWORK>",

    # --- 2. MAC Addresses in Varied Contexts ---
    "MAC: <NETWORK>02:42:ac:11:00:02</NETWORK>",
    "interface eth0 hardware address <NETWORK>00:1A:2B:3C:4D:5E</NETWORK>",
    "network adapter <NETWORK>A1:B2:C3:D4:E5:F6</NETWORK> disconnected",
    "container metadata shows mac_address=\"<NETWORK>02:00:00:00:00:01</NETWORK>\"",
    "DHCP request from <NETWORK>11:22:33:44:55:66</NETWORK>",
    "ARP cache: <HOSTINFO>192.168.1.1</HOSTINFO> is at <NETWORK>ff:ff:ff:ff:ff:ff</NETWORK>",
    "switch logs: port 5 link down, mac <NETWORK>aa:bb:cc:dd:ee:ff</NETWORK>",
    "host diagnostics: primary NIC <NETWORK>00-14-22-01-23-45</NETWORK>",
    "Found rogue device <NETWORK>00:50:56:c0:00:08</NETWORK> on VLAN 10",
    "Bluetooth MAC <NETWORK>98:D3:31:FD:3F:14</NETWORK> paired.",

    # --- 3. Composite Socket Addresses ---
    "Binding server to <HOSTINFO>0.0.0.0</HOSTINFO>:<NETWORK>8080</NETWORK> for incoming requests.",
    "Health check failed for <HOSTINFO>10.0.0.1</HOSTINFO>:<NETWORK>9090</NETWORK>.",
    "Blocked connection to <HOSTINFO>172.16.254.1</HOSTINFO>:<NETWORK>22</NETWORK>.",
    "The API is running at <HOSTINFO>127.0.0.1</HOSTINFO>:<NETWORK>5000</NETWORK>.",
    "Forwarding to <HOSTINFO>backend-service.local</HOSTINFO>:<NETWORK>8080</NETWORK>",
    "Proxy pass: http://<HOSTINFO>app.internal</HOSTINFO>:<NETWORK>3000</NETWORK>",
    "grpc://<HOSTINFO>192.168.0.5</HOSTINFO>:<NETWORK>50051</NETWORK>",
    "upstream cluster: <HOSTINFO>10.5.0.10</HOSTINFO>:<NETWORK>80</NETWORK>",
    "Node IP <HOSTINFO>192.168.1.100</HOSTINFO> mapped to host port <NETWORK>8081</NETWORK>",
    "Database host is <HOSTINFO>db.prod.lan</HOSTINFO>:<NETWORK>5432</NETWORK>",

    # --- 4. IPv6 Addresses and Ambiguous IP Contexts ---
    "Listen on IPv6 <HOSTINFO>::1</HOSTINFO> port <NETWORK>80</NETWORK>",
    "Node <HOSTINFO>fe80::1ff:fe23:4567:890a</HOSTINFO> joined the cluster.",
    "DNS resolution failed for <HOSTINFO>kubernetes.default.svc.cluster.local</HOSTINFO>",
    "<HOSTINFO>localhost</HOSTINFO> is mapped to <HOSTINFO>::1</HOSTINFO> in /etc/hosts",
    "Pinging <HOSTINFO>2001:0db8:85a3:0000:0000:8a2e:0370:7334</HOSTINFO>...",
    "Server responded from <HOSTINFO>2001:db8::ff00:42:8329</HOSTINFO>",
    "IPv6 route added via <HOSTINFO>fe80::1</HOSTINFO>",
    "Allowed origin: https://<HOSTINFO>[2001:db8::1]</HOSTINFO>:<NETWORK>443</NETWORK>",
    "Testing connectivity to <HOSTINFO>127.0.0.1</HOSTINFO> and <HOSTINFO>::1</HOSTINFO>",
    "My IP is <HOSTINFO>192.168.1.5</HOSTINFO> and public IP is <HOSTINFO>203.0.113.42</HOSTINFO>",

    # --- 5. Natural Language PII (Names & Addresses) ---
    "Send an SMS to <PII>+1-800-555-0199</PII> for 2FA.",
    "Customer <PII>Jane Doe</PII> lives at <PII>123 Main St, Springfield</PII>.",
    "The package was delivered to <PII>John Smith</PII> at <PII>42 Wallaby Way, Sydney</PII>.",
    "Please forward this email to <PII>Alice Johnson</PII> at <PII>alice.j@example.com</PII>.",
    "Contact <PII>Dr. Robert Paulson</PII> for emergency access.",
    "The new director, <PII>Mary Sue</PII>, approved the merge request.",
    "Billing address: <PII>1600 Pennsylvania Avenue NW, Washington, DC</PII>",
    "Mailing to <PII>Mr. Rogers</PII>, <PII>PO Box 12345, Neighborhood, NY</PII>.",
    "Hey <PII>Bob</PII>, did you review PR 42?",
    "User <PII>Emily Chen</PII> logged in from <HOSTINFO>10.0.0.5</HOSTINFO>.",

    # --- 6. Hard Negatives (Numeric/Identifiers/Public Domains) ---
    "The build completed in 2048 ms with 0 errors.",
    "Deployment id 5a2b3c4d5e failed because version 2.1.0 was not found.",
    "Run `chmod 777 script.sh` to fix permissions.",
    "Error 404: Page not found at /users/login",
    "Session UUID 123e4567-e89b-12d3-a456-426614174000 expired.",
    "The random seed is 424242 and instance index is 0.",
    "Traceback (most recent call last): File \"main.py\", line 42, in <module>",
    "seed = 424242",
    "request_id = 424242",
    "build_number = 424242",
    "process_id = 424242",
    "version = 424242",
    "timeout = 424242",
    "Visit www.example.com for more documentation.",
    "See RFC 1918 at tools.ietf.org",

    # --- 7. Full Connection Strings ---
    "Database URL is postgres://<PII>admin_user</PII>:<SECRET>super_secret_db_pass_123</SECRET>@<HOSTINFO>db-cluster.internal</HOSTINFO>:<NETWORK>5432</NETWORK>/prod_db",
    "Connecting to redis via redis://:<SECRET>cache_token_xyz987</SECRET>@<HOSTINFO>10.5.0.20</HOSTINFO>:<NETWORK>6379</NETWORK>",
    "Use amqp://<HOSTINFO>queue.local</HOSTINFO>:<NETWORK>5672</NETWORK> for RabbitMQ.",
    "mysql -h <HOSTINFO>192.168.10.5</HOSTINFO> -P <NETWORK>3306</NETWORK> -u <PII>root</PII> -p<SECRET>p@ssw0rd123!</SECRET>",
    "mongodb+srv://<PII>app_user</PII>:<SECRET>m0ng0p@ss</SECRET>@<HOSTINFO>cluster0.mongodb.net</HOSTINFO>/test",
    "Server=<HOSTINFO>sql.mycorp.local</HOSTINFO>;Database=HR;User Id=<PII>hr_admin</PII>;Password=<SECRET>hr_p@ss_word</SECRET>;",
    "jdbc:mysql://<HOSTINFO>10.0.1.25</HOSTINFO>:<NETWORK>3306</NETWORK>/mydb?user=<PII>admin</PII>&password=<SECRET>secret123</SECRET>",
    "elasticsearch://<PII>elastic</PII>:<SECRET>changeme</SECRET>@<HOSTINFO>192.168.1.150</HOSTINFO>:<NETWORK>9200</NETWORK>",
    "ftp://<PII>anonymous</PII>:<SECRET>anon@example.com</SECRET>@<HOSTINFO>ftp.example.com</HOSTINFO>/pub/file.txt",
    "ssh://<PII>git</PII>@<HOSTINFO>github.com</HOSTINFO>:<NETWORK>22</NETWORK>/repo.git",

    # --- 8. Code & Logs with Varied SECRET Contexts ---
    "2026-10-15 [WARN] User <PII>j.smith@company.com</PII> failed login from <HOSTINFO>172.16.254.1</HOSTINFO>",
    "const AWS_KEY = '<SECRET>AKIAIOSFODNN7EXAMPLE</SECRET>'; const REGION = 'us-west-2';",
    "export SLACK_TOKEN=\"<SECRET>xoxb-dummy-test-token-sample-1234567890</SECRET>\"",
    "The JWT token is <SECRET>eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c</SECRET>",
    "Basic auth header: Basic <SECRET>YWxhZGRpbjpvcGVuc2VzYW1l</SECRET>",
    "Stripe publishable key: <SECRET>pk_test_dummy_publishable_key_sample_1234</SECRET>",
    "GitHub personal access token: <SECRET>ghp_dummy_sample_github_token_for_tests_123</SECRET>",
    "Authorization: Bearer <SECRET>ya29.a0AfH6SMD...123456789</SECRET>",
    "X-Api-Key: <SECRET>abc123def456ghi789jkl</SECRET>",
    "client_secret = <SECRET>super-secret-oauth2-token</SECRET>",
    "We discovered a leaked key <SECRET>AKIAIOSFODNN7EXAMPLE</SECRET> in the repository.",

    # --- 9. Additional Multi-Entity and Ambiguous Patterns ---
    "Network <NETWORK>10.0.0.0/8</NETWORK> is routed via gateway <HOSTINFO>192.168.1.254</HOSTINFO>.",
    "Admin access from <HOSTINFO>192.168.1.100</HOSTINFO> to port <NETWORK>443</NETWORK> was blocked.",
    "Firewall rule: allow subnet <NETWORK>10.0.0.0/8</NETWORK> to reach <HOSTINFO>gateway.corp.local</HOSTINFO>",
    "User <PII>john.doe@corp.test</PII> executed command on <HOSTINFO>server-01</HOSTINFO>.",
    "Failed SSH to <HOSTINFO>10.10.10.10</HOSTINFO> as <PII>root</PII>.",
    "curl -X POST https://<HOSTINFO>api.stripe.com</HOSTINFO>/v1/charges -u <SECRET>sk_test_dummy_secret_key_sample_12345678</SECRET>: -d amount=2000",
    "Rotate API key <SECRET>old_key_123</SECRET> for user <PII>alice</PII>.",
    "Host <HOSTINFO>10.0.2.15</HOSTINFO> (MAC <NETWORK>08:00:27:11:22:33</NETWORK>) leased IP for 86400 seconds.",
    "Sent notification to <PII>bob@example.com</PII> about server <HOSTINFO>web.local</HOSTINFO> downtime.",
    "Received signal 15 on port <NETWORK>8080</NETWORK>."
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
    import re
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
    out_path = CHALLENGE_DIR / "challenge.jsonl"
    
    examples = []
    for text in CHALLENGE_TEXTS:
        ex = tokenize_and_tag(text)
        examples.append(ex)
        
    with open(out_path, "w", encoding="utf-8") as f:
        for ex in examples:
            f.write(json.dumps(ex) + "\n")
            
    print(f"Created challenge set with {len(examples)} examples at {out_path}")

if __name__ == "__main__":
    main()

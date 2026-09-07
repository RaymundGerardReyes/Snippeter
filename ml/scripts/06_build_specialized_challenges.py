import json
import os
import sys
import random
from pathlib import Path

BASE_DIR = Path(__file__).parent.parent
sys.path.insert(0, str(BASE_DIR))

from dataset.generator import generate_ipv6, generate_port, _render_bracketed_ipv6, generate_hostname, generate_ipv4, generate_secret, _render_db_connection, _render_composite_socket, _render_ssh_connection, _render_generic_host_port, EntityValue
from dataset.minimal_pairs import generate_minimal_pairs
from dataset.annotations import character_spans_to_bio

# Paths
CHALLENGE_DIR = Path(__file__).parent.parent / "data" / "challenge"

def create_boundary_challenge():
    """Focuses on trailing punctuation, bracketed IPs, zero-padded vs compressed IPv6, and FQDNs."""
    rng = random.Random(999)
    # Minimal pairs generator covers bracketed IPv6, trailing punctuation, etc.
    rendered_examples = generate_minimal_pairs(base_seed=10000, count=500)
    
    examples = []
    for ex in rendered_examples:
        bio = character_spans_to_bio(ex)
        examples.append({
            "text": ex.text,
            "tokens": bio.tokens,
            "ner_tags": bio.ner_tags
        })

    out_file = CHALLENGE_DIR / "challenge_boundary.jsonl"
    with open(out_file, "w", encoding="utf-8") as f:
        for ex in examples:
            f.write(json.dumps(ex) + "\n")
    print(f"Wrote {len(examples)} examples to {out_file}")

def create_composite_challenge():
    """Focuses on DB connection strings, URIs, and FTP credentials where PII/SECRET/HOSTINFO/NETWORK are dense."""
    rng = random.Random(1001)
    rendered_examples = []
    
    for i in range(250):
        # DB Connection strings
        user_names = ["admin", "root", "user", "app_user", "postgres", "db_admin", "developer"]
        user_ent = EntityValue(f"db_user_{i}", rng.choice(user_names), "PII", "name")
        pass_ent = generate_secret(rng, i)
        host_ent = generate_hostname(rng, i) if rng.random() > 0.5 else generate_ipv4(rng, i)
        port_ent = generate_port(rng, i)
        rendered_examples.append(_render_db_connection(user_ent, pass_ent, host_ent, port_ent, "composite", i, rng))
        
        # Sockets
        host2 = generate_ipv4(rng, i+100) if rng.random() > 0.5 else generate_ipv6(rng, i+100)
        port2 = generate_port(rng, i+100)
        rendered_examples.append(_render_composite_socket(host2, port2, "composite", i+100, rng))
        
        # Bracketed IPv6 sockets
        host3 = generate_ipv6(rng, i+200)
        port3 = generate_port(rng, i+200)
        rendered_examples.append(_render_bracketed_ipv6(host3, port3, "composite", i+200, rng))

        # SSH Connections
        host4 = generate_ipv4(rng, i+300) if rng.random() > 0.5 else generate_hostname(rng, i+300)
        port4 = generate_port(rng, i+300)
        rendered_examples.append(_render_ssh_connection(user_ent, host4, port4, "composite", i+300, rng))

        # Generic Host/Port (URIs, etc)
        host5 = generate_ipv4(rng, i+400) if rng.random() > 0.5 else generate_hostname(rng, i+400)
        port5 = generate_port(rng, i+400)
        rendered_examples.append(_render_generic_host_port(host5, port5, "composite", i+400, rng))

    examples = []
    for ex in rendered_examples:
        try:
            bio = character_spans_to_bio(ex)
            examples.append({
                "text": ex.text,
                "tokens": bio.tokens,
                "ner_tags": bio.ner_tags
            })
        except:
            continue

    out_file = CHALLENGE_DIR / "challenge_composite.jsonl"
    with open(out_file, "w", encoding="utf-8") as f:
        for ex in examples:
            f.write(json.dumps(ex) + "\n")
    print(f"Wrote {len(examples)} examples to {out_file}")

if __name__ == "__main__":
    os.makedirs(CHALLENGE_DIR, exist_ok=True)
    create_boundary_challenge()
    create_composite_challenge()

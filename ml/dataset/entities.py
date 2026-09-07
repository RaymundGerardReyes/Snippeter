"""
ml/dataset/entities.py

Typed, combinatorial generators for entities.
Each generator produces a specific form of an entity carrying its taxonomy category,
using mathematical randomness to guarantee near-infinite uniqueness.
"""

from __future__ import annotations
import random
import string
from dataclasses import dataclass
from typing import List

@dataclass
class EntityValue:
    value_id: str
    text: str
    category: str
    form: str

def _rand_str(rng: random.Random, length: int, chars: str = string.ascii_letters + string.digits) -> str:
    return "".join(rng.choices(chars, k=length))

def generate_port(rng: random.Random, instance_idx: int) -> EntityValue:
    # Mix common default ports with random high ports
    common = ["5432", "6379", "8080", "8443", "3306", "27017", "9200", "2181", "4369", "80", "443", "22", "5000", "3000"]
    if rng.random() < 0.3:
        p = rng.choice(common)
    else:
        p = str(rng.randint(1024, 65535))
    return EntityValue(f"port_{instance_idx}", p, "NETWORK", "port")

def generate_mac(rng: random.Random, instance_idx: int) -> EntityValue:
    # Use random hex for near-infinite MACs
    sep = rng.choice([":", "-"] * 4 + [""])
    octets = [f"{rng.randint(0,255):02x}" for _ in range(6)]
    if rng.random() < 0.2:
        octets = [o.upper() for o in octets]
    return EntityValue(f"mac_{instance_idx}", sep.join(octets), "NETWORK", "mac")

def generate_ipv4(rng: random.Random, instance_idx: int) -> EntityValue:
    ip = f"{rng.randint(1,255)}.{rng.randint(0,255)}.{rng.randint(0,255)}.{rng.randint(1,255)}"
    return EntityValue(f"ipv4_{instance_idx}", ip, "HOSTINFO", "ipv4")

def generate_ipv6(rng: random.Random, instance_idx: int) -> EntityValue:
    r = rng.random()
    if r < 0.1:
        return EntityValue(f"ipv6_{instance_idx}", "::1", "HOSTINFO", "ipv6")
    elif r < 0.2:
        return EntityValue(f"ipv6_{instance_idx}", "::", "HOSTINFO", "ipv6")
        
    segments = [f"{rng.randint(0,65535):x}" for _ in range(8)]
    
    r2 = rng.random()
    if r2 < 0.2:
        # Full 8-segment zero-padded IPv6
        segments = [f"{rng.randint(0, 65535):04x}" for _ in range(8)]
        val = ":".join(segments)
    elif r2 < 0.4:
        # Embedded IPv4 (e.g., ::ffff:192.168.1.1)
        ipv4 = f"{rng.randint(1,255)}.{rng.randint(0,255)}.{rng.randint(0,255)}.{rng.randint(1,255)}"
        val = f"::ffff:{ipv4}"
    elif r2 < 0.7:
        # Compressed IPv6 (replace largest consecutive zero run with ::, simplified here)
        idx = rng.randint(1, 5)
        length = rng.randint(1, 3)
        for i in range(length):
            segments[idx+i] = ""
        val = ":".join(segments).replace(":::", "::")
        # clean up multiple ::
        while ":::" in val: val = val.replace(":::", "::")
    else:
        # standard 8-segment with no leading zeros
        val = ":".join(segments)
        
    return EntityValue(f"ipv6_{instance_idx}", val, "HOSTINFO", "ipv6")

def generate_hostname(rng: random.Random, instance_idx: int) -> EntityValue:
    prefixes = ["db", "api", "web", "cache", "redis", "auth", "svc", "node", "app", "worker"]
    envs = ["prod", "dev", "test", "staging", "internal"]
    tlds = ["com", "org", "net", "io", "local", "lan", "corp", "svc.cluster.local", "compute.internal"]
    
    parts = []
    if rng.random() < 0.2:
        # Kubernetes / Cloud style FQDN
        domain = rng.choice([
            "kubernetes.default.svc.cluster.local",
            "db-cluster.internal",
            "sql.mycorp.local",
            "us-east-1.compute.internal",
            "api.example.org",
            "docs.example.com",
        ])
    else:
        if rng.random() < 0.7:
            parts.append(rng.choice(prefixes))
            if rng.random() < 0.5:
                parts.append(f"{rng.randint(1, 999):02d}")
        else:
            parts.append(_rand_str(rng, rng.randint(4, 10), string.ascii_lowercase))
            
        if rng.random() < 0.4:
            parts.append(rng.choice(envs))
            
        domain = "-".join(parts) + "." + rng.choice(tlds)
        if rng.random() < 0.05:
            domain = "localhost"
    return EntityValue(f"host_{instance_idx}", domain, "HOSTINFO", "hostname")

_FIRST_NAMES = ["James", "Mary", "John", "Patricia", "Robert", "Jennifer", "Michael", "Linda", "William", "Elizabeth", "David", "Barbara", "Richard", "Susan", "Joseph", "Jessica", "Thomas", "Sarah", "Charles", "Karen", "Alice", "Bob", "Charlie"]
_LAST_NAMES = ["Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez", "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson", "Thomas", "Taylor", "Moore", "Jackson", "Martin", "Lee", "Perez", "Thompson", "White", "Harris"]

def generate_person_name(rng: random.Random, instance_idx: int) -> EntityValue:
    f = rng.choice(_FIRST_NAMES)
    l = rng.choice(_LAST_NAMES)
    r = rng.random()
    if r < 0.2:
        # Single first name (e.g. Charlie, Alice, Bob)
        name = f
    elif r < 0.35:
        # Title + Full name (e.g. Dr. Gregory House, Dr. Robert Paulson)
        title = rng.choice(["Dr.", "Mr.", "Mrs.", "Ms.", "Prof."])
        name = f"{title} {f} {l}"
    else:
        name = f"{f} {l}"
    return EntityValue(f"name_{instance_idx}", name, "PII", "name")

_STREET_NAMES = ["Main", "Oak", "Pine", "Maple", "Cedar", "Elm", "Washington", "Lake", "Hill", "Park", "Meadow", "Valley"]
_STREET_TYPES = ["St", "Ave", "Blvd", "Rd", "Ln", "Dr", "Way", "Court"]
_CITIES = ["Springfield", "Riverside", "Dayton", "Franklin", "Clinton", "Madison", "Chester", "Marion", "Greenville", "Washington"]

def generate_address(rng: random.Random, instance_idx: int) -> EntityValue:
    num = rng.randint(1, 9999)
    street = rng.choice(_STREET_NAMES)
    st_type = rng.choice(_STREET_TYPES)
    city = rng.choice(_CITIES)
    zipcode = f"{rng.randint(10000, 99999)}"
    
    formats = [
        f"{num} {street} {st_type}, {city}",
        f"{num} {street} {st_type}",
        f"{num} {street} {st_type}, {city} {zipcode}"
    ]
    return EntityValue(f"address_{instance_idx}", rng.choice(formats), "PII", "address")

def generate_phone(rng: random.Random, instance_idx: int) -> EntityValue:
    area = rng.randint(200, 999)
    prefix = rng.randint(200, 999)
    line = rng.randint(1000, 9999)
    
    formats = [
        f"{area}-{prefix}-{line}",
        f"({area}) {prefix}-{line}",
        f"+1-{area}-{prefix}-{line}",
        f"{area}.{prefix}.{line}"
    ]
    return EntityValue(f"phone_{instance_idx}", rng.choice(formats), "PII", "phone")

def generate_email(rng: random.Random, instance_idx: int) -> EntityValue:
    domain = rng.choice(["example.com", "test.org", "company.net", "corp.local", "secure-corp.com", "mail.invalid"])
    if rng.random() < 0.3:
        # Role-based local part (e.g. admin@, support@, info@)
        user = rng.choice(["admin", "support", "info", "contact", "billing", "security", "help", "sales"])
    else:
        f = rng.choice(_FIRST_NAMES).lower()
        l = rng.choice(_LAST_NAMES).lower()
        user = f"{f}.{l}{rng.randint(1,99) if rng.random() > 0.5 else ''}"
    email = f"{user}@{domain}"
    return EntityValue(f"email_{instance_idx}", email, "PII", "email")

def generate_secret(rng: random.Random, instance_idx: int) -> EntityValue:
    t = rng.randint(0, 6)
    if t == 0:
        # JWT-like
        secret = f"eyJ{_rand_str(rng, 20)}.{_rand_str(rng, 40)}.{_rand_str(rng, 43, string.ascii_letters + string.digits + '-_')}"
    elif t == 1:
        # Base64 credentials / Basic Auth
        secret = rng.choice(["dXNlcjpwYXNz", "YWRtaW46YWRtaW4xMjM=", "c2VjcmV0MTIz", "dGVzdDpwYXNzd29yZA=="])
        if rng.random() > 0.5:
            secret = _rand_str(rng, rng.choice([24, 32, 44]), string.ascii_letters + string.digits + "+/") + "="
    elif t == 2:
        # AWS style / GCP
        secret = f"AKIA{_rand_str(rng, 16, string.ascii_uppercase + string.digits)}"
    elif t == 3:
        # GitHub PAT
        prefix = rng.choice(["ghp_", "gho_", "ghu_", "ghs_", "github_pat_"])
        secret = f"{prefix}{_rand_str(rng, 36, string.ascii_letters + string.digits)}"
    elif t == 4:
        # Stripe / API key styles
        prefix = rng.choice(["sk_live_", "sk_test_", "ak_live_", "pk_test_", "api_key_"])
        secret = f"{prefix}{_rand_str(rng, 24, string.ascii_letters + string.digits)}"
    elif t == 5:
        # DB Password / Shell secrets with punctuation & symbols
        specials = ["s#p3rS3cr3t!", "r00tp@ss", "secret123", "P@ssw0rd!", "admin123", "hr_p@ss_word", "cache_token_xyz987", "m0ng0p@ss", "changeme", "anon@", "password", "test_secret"]
        secret = rng.choice(specials)
    else:
        # Hex / HMAC hashes
        secret = _rand_str(rng, rng.choice([32, 40, 64]), string.hexdigits.lower())
    return EntityValue(f"secret_{instance_idx}", secret, "SECRET", "token")

def generate_hard_negative(rng: random.Random, instance_idx: int, entity_type: str) -> EntityValue:
    if entity_type == "NETWORK":
        # Numbers that look like ports but aren't
        negs = [str(rng.randint(1, 999999)), str(rng.randint(1, 100)), f"{rng.randint(1,99)}.{rng.randint(1,99)}", "404", "500", "200", "424242", "2FA"]
        return EntityValue(f"neg_net_{instance_idx}", rng.choice(negs), "NEG", "neg_network")
    elif entity_type == "HOSTINFO":
        negs = [f"{_rand_str(rng, 5, string.ascii_lowercase)}.example.com", "docs.example.org", "tools.ietf.org", "www.w3.org"]
        return EntityValue(f"neg_host_{instance_idx}", rng.choice(negs), "NEG", "neg_hostinfo")
    elif entity_type == "SECRET":
        # UUIDs
        u = f"{_rand_str(rng, 8, string.hexdigits.lower())}-{_rand_str(rng, 4, string.hexdigits.lower())}-{_rand_str(rng, 4, string.hexdigits.lower())}-{_rand_str(rng, 4, string.hexdigits.lower())}-{_rand_str(rng, 12, string.hexdigits.lower())}"
        return EntityValue(f"neg_secret_{instance_idx}", rng.choice([u, "424242"]), "NEG", "neg_secret")
    elif entity_type == "SECRET_NEG":
        # Dummy placeholders that appear in secret contexts but are not real secrets
        secret_negs = ["REDACTED", "NULL", "YOUR_SECRET_HERE", "******", "password", "none", "hidden", "TBD"]
        return EntityValue(f"neg_sec_{instance_idx}", rng.choice(secret_negs), "NEG", "neg_secret")
    elif entity_type == "ROLE":
        # System usernames / roles - O, not PII
        role_negs = ["root", "admin", "user", "guest", "anonymous", "git", "www-data", "nobody", "daemon", "postgres", "mysql"]
        return EntityValue(f"neg_role_{instance_idx}", rng.choice(role_negs), "NEG", "neg_generic")
    elif entity_type == "PII_NEG":
        # Public entities or generic objects - O, not PII
        pii_negs = ["Acme Corp", "The Server", "the system", "noreply@example.com", "support@example.org", "Central Park", "Headquarters"]
        return EntityValue(f"neg_pii_{instance_idx}", rng.choice(pii_negs), "NEG", "neg_generic")
    else:
        negs = [f"v{rng.randint(1,9)}.{rng.randint(0,9)}.{rng.randint(0,9)}", f"build-{rng.randint(1000,9999)}", "timeout", "INFO", "WARNING", "424242"]
        return EntityValue(f"neg_generic_{instance_idx}", rng.choice(negs), "NEG", "neg_generic")

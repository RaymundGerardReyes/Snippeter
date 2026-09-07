"""
ml/dataset/generator.py

Dataset v1.4 Generation Engine
Implements the Entity -> Form -> Context hierarchy with dynamic padding.
"""
from __future__ import annotations
import random
from typing import List, Tuple

from .entities import (
    EntityValue,
    generate_port, generate_mac, generate_ipv4, generate_ipv6,
    generate_hostname, generate_person_name, generate_address,
    generate_email, generate_phone, generate_secret, generate_hard_negative
)
from .contexts import (
    get_port_contexts, get_mac_contexts, get_ip_contexts,
    get_hostname_contexts, get_name_contexts, get_address_contexts,
    get_email_contexts, get_phone_contexts, get_secret_contexts, get_generic_negative_contexts,
    ContextPadder
)
from .minimal_pairs import generate_minimal_pairs
from .annotations import Span, RenderedExample

def _render_from_template(
    template: str,
    entities: List[Tuple[str, EntityValue, str]],  # List of (placeholder, entity, label)
    group_id: str,
    family: str,
    seed: int,
    rng: random.Random
) -> RenderedExample:
    """
    Renders template by replacing placeholders, recording exact character spans in base_text,
    and then applying random padding.
    """
    placeholders = []
    for ph, ent, label in entities:
        pos = template.find(ph)
        if pos != -1:
            placeholders.append((pos, ph, ent, label))
    
    placeholders.sort(key=lambda x: x[0])
    
    base_text = ""
    last_pos = 0
    spans_in_base: List[Tuple[int, int, str]] = []
    
    for pos, ph, ent, label in placeholders:
        base_text += template[last_pos:pos]
        ent_start = len(base_text)
        base_text += ent.text
        ent_end = len(base_text)
        if label != "O" and ent.category != "NEG":
            spans_in_base.append((ent_start, ent_end, label))
        last_pos = pos + len(ph)
        
    base_text += template[last_pos:]
    
    padded_text, prefix_len = ContextPadder.apply_random_padding_with_offset(base_text, rng)
    
    final_spans = [
        Span(start=prefix_len + s, end=prefix_len + e, label=l)
        for s, e, l in spans_in_base
    ]
    
    return RenderedExample(
        text=padded_text,
        spans=sorted(final_spans, key=lambda s: s.start),
        template_id=group_id,
        family=family,
        generation_seed=seed,
        entity_value_ids=[ent.value_id for _, ent, _ in entities]
    )

def _render_single(
    base_template: str, 
    entity: EntityValue, 
    group_id: str, 
    seed: int,
    rng: random.Random
) -> RenderedExample:
    return _render_from_template(
        base_template,
        [("{ENTITY}", entity, entity.category)],
        group_id,
        entity.form,
        seed,
        rng
    )

def _render_composite_socket(
    host_entity: EntityValue, 
    port_entity: EntityValue, 
    group_id: str, 
    seed: int,
    rng: random.Random
) -> RenderedExample:
    """Render a composite HOSTINFO:PORT socket address with padding."""
    templates = [
        "server={HOST}:{PORT}",
        "tcp://{HOST}:{PORT}",
        "http://{HOST}:{PORT}/api/v1",
        "Binding server to {HOST}:{PORT} for incoming requests.",
        "Health check failed for {HOST}:{PORT}.",
        "Blocked connection to {HOST}:{PORT}.",
        "The API is running at {HOST}:{PORT}.",
        "Forwarding to {HOST}:{PORT}",
        "Proxy pass: http://{HOST}:{PORT}",
        "grpc://{HOST}:{PORT}",
        "upstream cluster: {HOST}:{PORT}",
        "Database host is {HOST}:{PORT}",
    ]
    template = rng.choice(templates)
    
    is_negative_ipv6 = False
    if rng.random() > 0.5 and ":" in host_entity.text and not host_entity.text.startswith("["):
        template = template.replace(":{PORT}", "")
        is_negative_ipv6 = True

    ents = [("{HOST}", host_entity, host_entity.category)]
    if not is_negative_ipv6:
        ents.append(("{PORT}", port_entity, port_entity.category))
        
    return _render_from_template(
        template,
        ents,
        group_id,
        "composite_socket",
        seed,
        rng
    )

def _render_db_connection(user_ent: EntityValue, pass_ent: EntityValue, host_ent: EntityValue, port_ent: EntityValue | None, group_id: str, seed: int, rng: random.Random) -> RenderedExample:
    templates = [
        "mongodb+srv://{USER}:{PASS}@{HOST}/test",
        "postgres://{USER}:{PASS}@{HOST}:{PORT}/prod",
        "mysql://{USER}:{PASS}@{HOST}:{PORT}/db",
        "jdbc:mysql://{HOST}:{PORT}/mydb?user={USER}&password={PASS}",
        "Server={HOST};Database=HR;User Id={USER};Password={PASS};",
    ]
    template = rng.choice(templates)
    is_negative_ipv6 = False
    if rng.random() > 0.5 and port_ent and ":" in host_ent.text and not host_ent.text.startswith("["):
        template = template.replace(":{PORT}", "")
        is_negative_ipv6 = True

    ents = [
        ("{USER}", user_ent, "PII"),
        ("{PASS}", pass_ent, "SECRET"),
        ("{HOST}", host_ent, "HOSTINFO")
    ]
    if port_ent and not is_negative_ipv6 and "{PORT}" in template:
        ents.append(("{PORT}", port_ent, "NETWORK"))
    elif "{PORT}" in template:
        template = template.replace("{PORT}", "5432")

    return _render_from_template(
        template,
        ents,
        group_id,
        "composite_db",
        seed,
        rng
    )

def _render_bracketed_ipv6(host_ent: EntityValue, port_ent: EntityValue, group_id: str, seed: int, rng: random.Random) -> RenderedExample:
    templates = [
        "https://[{HOST}]:{PORT}",
        "Allowed origin: https://[{HOST}]:{PORT}",
        "Listen on [{HOST}]",
        "bind [{HOST}]:{PORT}",
        "Testing connectivity to [{HOST}]",
    ]
    template = rng.choice(templates)
    ents = [("{HOST}", host_ent, "HOSTINFO")]
    if "{PORT}" in template:
        ents.append(("{PORT}", port_ent, "NETWORK"))
        
    return _render_from_template(
        template,
        ents,
        group_id,
        "composite_bracketed_ipv6",
        seed,
        rng
    )

def _render_ssh_connection(user_ent: EntityValue, host_ent: EntityValue, port_ent: EntityValue, group_id: str, seed: int, rng: random.Random) -> RenderedExample:
    templates = [
        "ssh {USER}@{HOST} -p {PORT}",
        "scp -P {PORT} file.txt {USER}@{HOST}:/tmp/",
        "sftp -P {PORT} {USER}@{HOST}"
    ]
    template = rng.choice(templates)
    ents = [
        ("{USER}", user_ent, "PII"),
        ("{HOST}", host_ent, "HOSTINFO"),
        ("{PORT}", port_ent, "NETWORK")
    ]
    return _render_from_template(
        template,
        ents,
        group_id,
        "composite_ssh",
        seed,
        rng
    )

def _render_generic_host_port(host_ent: EntityValue, port_ent: EntityValue, group_id: str, seed: int, rng: random.Random) -> RenderedExample:
    templates = [
        "http://{HOST}:{PORT}/api",
        "ws://{HOST}:{PORT}/socket",
        "tcp://{HOST}:{PORT}",
        "Connecting to {HOST}:{PORT}",
        "Proxy pass: {HOST}:{PORT}"
    ]
    
    is_error_template = False
    if rng.random() > 0.5:
        templates.append("Host {HOST} returned error {PORT}")
        
    template = rng.choice(templates)
    if "error" in template:
        is_error_template = True

    is_negative_ipv6 = False
    if rng.random() > 0.5 and ":" in host_ent.text and not host_ent.text.startswith("["):
        template = template.replace(":{PORT}", "")
        is_negative_ipv6 = True

    ents = [("{HOST}", host_ent, "HOSTINFO")]
    if not is_negative_ipv6 and not is_error_template and "{PORT}" in template:
        ents.append(("{PORT}", port_ent, "NETWORK"))
    elif is_error_template and "{PORT}" in template:
        ents.append(("{PORT}", port_ent, "O"))

    return _render_from_template(
        template,
        ents,
        group_id,
        "composite_host_port",
        seed,
        rng
    )

def generate_all_hierarchical(instances_per_form: int, base_seed: int) -> List[RenderedExample]:
    """
    Generate dataset v1.8 using Entity -> Form -> Context hierarchy.
    Targets:
      NETWORK  : ~9000 spans
      HOSTINFO : ~9000 spans (includes bracketed IPv6 & FQDNs)
      SECRET   : ~9000 spans (rich deep secret syntax families)
      PII      : ~9000 spans (includes role emails & DB usernames)
      NEG      : ~3000 hard-negative examples (role names, UUIDs, seeds)
    """
    examples = []
    rng = random.Random(base_seed)
    
    # ── Per-form instance counts ─────────────────────────────────────────────
    # Target ~9000 spans per class while keeping context variety.
    net_inst  = 9000 // len(get_port_contexts() + get_mac_contexts())
    host_inst = 9000 // len(get_ip_contexts() + get_hostname_contexts())
    pii_inst  = 9000 // len(get_name_contexts() + get_address_contexts() + get_email_contexts() + get_phone_contexts())
    sec_inst  = 9000 // len(get_secret_contexts())
    
    # Dramatically increase Hard Negatives to combat False Positives (especially HOSTINFO precision)
    neg_inst  = 12000 // len(get_generic_negative_contexts())

    # 1. NETWORK (Ports + MACs)
    for ctx_idx, ctx in enumerate(get_port_contexts()):
        group_id = f"port_ctx_{ctx_idx}"
        for i in range(net_inst):
            ent = generate_port(rng, i)
            examples.append(_render_single(ctx, ent, group_id, base_seed + i, rng))
            
    for ctx_idx, ctx in enumerate(get_mac_contexts()):
        group_id = f"mac_ctx_{ctx_idx}"
        for i in range(net_inst):
            ent = generate_mac(rng, i)
            examples.append(_render_single(ctx, ent, group_id, base_seed + i, rng))

    # 2. HOSTINFO (IPs + Hostnames)
    for ctx_idx, ctx in enumerate(get_ip_contexts()):
        group_id = f"ip_ctx_{ctx_idx}"
        for i in range(host_inst):
            if rng.random() < 0.15:
                ent = generate_hard_negative(rng, i, "NETWORK")
            else:
                ent = generate_ipv4(rng, i) if rng.random() > 0.5 else generate_ipv6(rng, i)
            examples.append(_render_single(ctx, ent, group_id, base_seed + i, rng))

    for ctx_idx, ctx in enumerate(get_hostname_contexts()):
        group_id = f"host_ctx_{ctx_idx}"
        for i in range(host_inst):
            if rng.random() < 0.15:
                ent = generate_hard_negative(rng, i, "HOSTINFO")
            else:
                ent = generate_hostname(rng, i)
            examples.append(_render_single(ctx, ent, group_id, base_seed + i, rng))

    # 3. PII (Names, Addresses, Emails)
    for ctx_idx, ctx in enumerate(get_name_contexts()):
        group_id = f"name_ctx_{ctx_idx}"
        for i in range(pii_inst):
            # Inject 15% hard PII negatives into name contexts
            if rng.random() < 0.15:
                ent = generate_hard_negative(rng, i, "PII_NEG")
            else:
                ent = generate_person_name(rng, i)
            examples.append(_render_single(ctx, ent, group_id, base_seed + i, rng))

    for ctx_idx, ctx in enumerate(get_address_contexts()):
        group_id = f"addr_ctx_{ctx_idx}"
        for i in range(pii_inst):
            ent = generate_address(rng, i)
            examples.append(_render_single(ctx, ent, group_id, base_seed + i, rng))
            
    for ctx_idx, ctx in enumerate(get_email_contexts()):
        group_id = f"email_ctx_{ctx_idx}"
        for i in range(pii_inst):
            # 10% fake generic emails that are not PII
            if rng.random() < 0.10:
                ent = generate_hard_negative(rng, i, "PII_NEG")
            else:
                ent = generate_email(rng, i)
            examples.append(_render_single(ctx, ent, group_id, base_seed + i, rng))

    for ctx_idx, ctx in enumerate(get_phone_contexts()):
        group_id = f"phone_ctx_{ctx_idx}"
        for i in range(pii_inst):
            ent = generate_phone(rng, i)
            examples.append(_render_single(ctx, ent, group_id, base_seed + i, rng))

    # 4. SECRETS
    for ctx_idx, ctx in enumerate(get_secret_contexts()):
        group_id = f"secret_ctx_{ctx_idx}"
        for i in range(sec_inst):
            if rng.random() < 0.15:
                ent = generate_hard_negative(rng, i, "SECRET_NEG")
            else:
                ent = generate_secret(rng, i)
            examples.append(_render_single(ctx, ent, group_id, base_seed + i, rng))

    # 5. HARD NEGATIVES (numbers, identifiers, role names)
    for ctx_idx, ctx in enumerate(get_generic_negative_contexts()):
        group_id = f"neg_ctx_{ctx_idx}"
        for i in range(neg_inst):
            # Heavily weight HOSTINFO and NETWORK negatives to penalize structural False Positives
            t = rng.choices(
                population=["NETWORK", "HOSTINFO", "SECRET", "GENERIC", "ROLE", "PII_NEG"],
                weights=[0.3, 0.35, 0.1, 0.1, 0.1, 0.05],
                k=1
            )[0]
            ent = generate_hard_negative(rng, i, t)
            examples.append(_render_single(ctx, ent, group_id, base_seed + i, rng))

    # 6. COMPOSITE SOCKETS & DB CONNECTIONS & BRACKETED IPV6
    for i in range(1500):
        group_id = f"composite_socket_{i % 40}"
        host_ent = generate_ipv4(rng, i) if rng.random() > 0.5 else generate_hostname(rng, i)
        port_ent = generate_port(rng, i)
        examples.append(_render_composite_socket(host_ent, port_ent, group_id, base_seed + i, rng))

    for i in range(1200):
        group_id = f"composite_db_{i % 30}"
        user_names = ["admin", "root", "user", "app_user", "postgres", "db_admin", "developer"]
        user_ent = EntityValue(f"db_user_{i}", rng.choice(user_names), "PII", "name")
        pass_ent = generate_secret(rng, i)
        host_ent = generate_hostname(rng, i) if rng.random() > 0.5 else generate_ipv4(rng, i)
        port_ent = generate_port(rng, i)
        examples.append(_render_db_connection(user_ent, pass_ent, host_ent, port_ent, group_id, base_seed + i, rng))

    for i in range(800):
        group_id = f"composite_ipv6_{i % 20}"
        host_ent = generate_ipv6(rng, i)
        port_ent = generate_port(rng, i)
        examples.append(_render_bracketed_ipv6(host_ent, port_ent, group_id, base_seed + i, rng))

    for i in range(1000):
        group_id = f"composite_ssh_{i % 20}"
        user_names = ["admin", "root", "user", "app_user", "postgres", "db_admin", "developer"]
        user_ent = EntityValue(f"db_user_{i}", rng.choice(user_names), "PII", "name")
        host_ent = generate_ipv4(rng, i) if rng.random() > 0.5 else generate_hostname(rng, i)
        port_ent = generate_port(rng, i)
        examples.append(_render_ssh_connection(user_ent, host_ent, port_ent, group_id, base_seed + i, rng))

    for i in range(2000):
        group_id = f"composite_host_port_{i % 20}"
        host_ent = generate_ipv4(rng, i) if rng.random() > 0.5 else generate_hostname(rng, i)
        port_ent = generate_port(rng, i)
        examples.append(_render_generic_host_port(host_ent, port_ent, group_id, base_seed + i, rng))

    # 7. Minimal Pairs (Boundary Condition Training)
    mp_examples = generate_minimal_pairs(base_seed, 2000)
    examples.extend(mp_examples)

    rng.shuffle(examples)
    return examples

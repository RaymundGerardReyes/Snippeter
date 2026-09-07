import random
from typing import List
from .annotations import RenderedExample, Span
from .entities import generate_ipv4, generate_ipv6, generate_hostname, generate_mac, generate_person_name, generate_address, generate_email, generate_secret

def generate_minimal_pairs(base_seed: int, count: int = 1000) -> List[RenderedExample]:
    examples = []
    rng = random.Random(base_seed)
    
    # 1. Bracketed vs non-bracketed IPv6
    for i in range(count // 5):
        ipv6 = generate_ipv6(rng, i)
        
        # Example 1: Non-bracketed
        t1 = f"Origin: https://{ipv6.text}:443"
        start1 = t1.find(ipv6.text)
        port_start = t1.find("443", start1 + len(ipv6.text))
        examples.append(RenderedExample(
            text=t1,
            spans=[
                Span(start1, start1 + len(ipv6.text), "HOSTINFO"),
                Span(port_start, port_start + 3, "NETWORK")
            ],
            template_id="mp_ipv6_nobracket",
            family="minimal_pair",
            generation_seed=base_seed + i,
            entity_value_ids=[ipv6.value_id]
        ))
        
        # Example 2: Bracketed
        t2 = f"Origin: https://[{ipv6.text}]:443"
        start2 = t2.find(ipv6.text)
        port_start2 = t2.find("443", start2 + len(ipv6.text))
        examples.append(RenderedExample(
            text=t2,
            spans=[
                Span(start2, start2 + len(ipv6.text), "HOSTINFO"),
                Span(port_start2, port_start2 + 3, "NETWORK")
            ],
            template_id="mp_ipv6_bracket",
            family="minimal_pair",
            generation_seed=base_seed + i,
            entity_value_ids=[ipv6.value_id]
        ))
        
    # 2. Punctuation trailing vs no trailing (IPs)
    for i in range(count // 5):
        ip = generate_ipv4(rng, i) if rng.random() > 0.5 else generate_ipv6(rng, i)
        
        # No trailing
        t1 = f"Pinging {ip.text}"
        start1 = t1.find(ip.text)
        examples.append(RenderedExample(
            text=t1,
            spans=[Span(start1, start1 + len(ip.text), "HOSTINFO")],
            template_id="mp_ip_nopunct",
            family="minimal_pair",
            generation_seed=base_seed + i,
            entity_value_ids=[ip.value_id]
        ))
        
        # Trailing
        t2 = f"Pinging {ip.text}..."
        start2 = t2.find(ip.text)
        examples.append(RenderedExample(
            text=t2,
            spans=[Span(start2, start2 + len(ip.text), "HOSTINFO")],
            template_id="mp_ip_punct",
            family="minimal_pair",
            generation_seed=base_seed + i,
            entity_value_ids=[ip.value_id]
        ))
        
    # 3. Punctuation trailing vs no trailing (PII Names)
    for i in range(count // 5):
        name = generate_person_name(rng, i)
        
        # No trailing
        t1 = f"Hello {name.text}"
        start1 = t1.find(name.text)
        examples.append(RenderedExample(
            text=t1,
            spans=[Span(start1, start1 + len(name.text), "PII")],
            template_id="mp_name_nopunct",
            family="minimal_pair",
            generation_seed=base_seed + i,
            entity_value_ids=[name.value_id]
        ))
        
        # Trailing comma
        t2 = f"Hello {name.text}, welcome"
        start2 = t2.find(name.text)
        examples.append(RenderedExample(
            text=t2,
            spans=[Span(start2, start2 + len(name.text), "PII")],
            template_id="mp_name_punct",
            family="minimal_pair",
            generation_seed=base_seed + i,
            entity_value_ids=[name.value_id]
        ))

    # 4. MAC address with and without punctuation trailing
    for i in range(count // 5):
        mac = generate_mac(rng, i)
        
        # No trailing
        t1 = f"The MAC is {mac.text}"
        start1 = t1.find(mac.text)
        examples.append(RenderedExample(
            text=t1,
            spans=[Span(start1, start1 + len(mac.text), "NETWORK")],
            template_id="mp_mac_nopunct",
            family="minimal_pair",
            generation_seed=base_seed + i,
            entity_value_ids=[mac.value_id]
        ))
        
        # Trailing period
        t2 = f"The MAC is {mac.text}."
        start2 = t2.find(mac.text)
        examples.append(RenderedExample(
            text=t2,
            spans=[Span(start2, start2 + len(mac.text), "NETWORK")],
            template_id="mp_mac_punct",
            family="minimal_pair",
            generation_seed=base_seed + i,
            entity_value_ids=[mac.value_id]
        ))
        
    # 5. Secret vs PII Email (anon@example.com)
    for i in range(count // 5):
        email = generate_email(rng, i)
        
        # Normal email PII
        t1 = f"Contact {email.text} for details."
        start1 = t1.find(email.text)
        examples.append(RenderedExample(
            text=t1,
            spans=[Span(start1, start1 + len(email.text), "PII")],
            template_id="mp_email_pii",
            family="minimal_pair",
            generation_seed=base_seed + i,
            entity_value_ids=[email.value_id]
        ))
        
        # Email as FTP secret
        t2 = f"ftp://anonymous:{email.text}@ftp.example.com/"
        start2 = t2.find(email.text)
        examples.append(RenderedExample(
            text=t2,
            spans=[
                Span(start2, start2 + len(email.text), "SECRET"),
                Span(t2.find("ftp.example.com"), t2.find("ftp.example.com") + len("ftp.example.com"), "HOSTINFO")
            ],
            template_id="mp_email_secret",
            family="minimal_pair",
            generation_seed=base_seed + i,
            entity_value_ids=[email.value_id]
        ))

    # 6. IPv6 Port Boundary Minimal Pairs (The Swallowing Port Problem)
    # We use varied prefixes to prevent the model from anchoring on a single context like "Forwarding to".
    ipv6_prefixes = [
        "tcp://", "http://", "https://", "ws://", 
        "Proxy pass: http://", "upstream cluster: ", "Database host is ", 
        "Forwarding to ", "Connecting to ", "The API is running at ",
        "bind ", "Listen on "
    ]
    
    for i in range(count // 5):
        # Generate an IPv6 that ends in a port-like segment
        port_like = rng.randint(1000, 9999)
        base_ipv6 = f"2001:db8:3333:4444:5555:6666:7777:{port_like}"
        prefix = rng.choice(ipv6_prefixes)
        
        # 6a. Negative example: No port, the port-like segment belongs to the host
        t1 = f"{prefix}{base_ipv6}"
        start1 = t1.find(base_ipv6)
        examples.append(RenderedExample(
            text=t1,
            spans=[Span(start1, start1 + len(base_ipv6), "HOSTINFO")],
            template_id="mp_ipv6_noport",
            family="minimal_pair_ipv6_port",
            generation_seed=base_seed + i,
            entity_value_ids=["mp_ipv6_port_base"]
        ))
        
        # 6b. Positive example: Same IPv6, but with an actual port
        real_port = rng.randint(1024, 65535)
        t2 = f"{prefix}{base_ipv6}:{real_port}"
        start2 = t2.find(base_ipv6)
        p_start2 = t2.find(str(real_port), start2 + len(base_ipv6))
        examples.append(RenderedExample(
            text=t2,
            spans=[
                Span(start2, start2 + len(base_ipv6), "HOSTINFO"),
                Span(p_start2, p_start2 + len(str(real_port)), "NETWORK")
            ],
            template_id="mp_ipv6_withport",
            family="minimal_pair_ipv6_port",
            generation_seed=base_seed + i,
            entity_value_ids=["mp_ipv6_port_base", "mp_ipv6_real_port"]
        ))
        
        # 6c. Positive bracketed example
        t3 = f"{prefix}[{base_ipv6}]:{real_port}"
        start3 = t3.find(base_ipv6)
        p_start3 = t3.find(str(real_port), start3 + len(base_ipv6))
        examples.append(RenderedExample(
            text=t3,
            spans=[
                Span(start3, start3 + len(base_ipv6), "HOSTINFO"),
                Span(p_start3, p_start3 + len(str(real_port)), "NETWORK")
            ],
            template_id="mp_ipv6_bracket_withport",
            family="minimal_pair_ipv6_port",
            generation_seed=base_seed + i,
            entity_value_ids=["mp_ipv6_port_base", "mp_ipv6_real_port"]
        ))

    return examples

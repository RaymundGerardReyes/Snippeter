"""
ml/dataset/contexts.py

Context generators and ContextPadder for Dataset v1.4.
"""
from __future__ import annotations
from typing import List
import random
import string

def _rand_str(rng: random.Random, length: int) -> str:
    return "".join(rng.choices(string.ascii_lowercase, k=length))

def get_port_contexts() -> List[str]:
    return [
        "port = {ENTITY}",
        "port: {ENTITY}",
        "PORT={ENTITY}",
        "serverPort = {ENTITY}",
        "httpPort = {ENTITY}",
        "redis.port = {ENTITY}",
        "listen({ENTITY})",
        "app.listen({ENTITY})",
        ".listen({ENTITY})",
        "listen {ENTITY} ssl",
        "\"port\": {ENTITY}",
        "config[\"port\"] = {ENTITY}",
        "config.set('port', {ENTITY})",
        "Target port is {ENTITY} for the container",
    ]

def get_mac_contexts() -> List[str]:
    return [
        "MAC: {ENTITY}",
        "mac_address=\"{ENTITY}\"",
        "interface eth0 hardware address {ENTITY}",
        "network adapter {ENTITY} disconnected",
        "DHCP request from {ENTITY}",
        "switch logs: port 5 link down, mac {ENTITY}",
        "host diagnostics: primary NIC {ENTITY}",
        "Found rogue device {ENTITY} on VLAN 10",
        "Bluetooth MAC {ENTITY} paired.",
        "ARP cache : gateway is at {ENTITY}",
        "ARP reply for gateway is {ENTITY}",
    ]

def get_ip_contexts() -> List[str]:
    return [
        "Host {ENTITY} joined the cluster.",
        "IP address: {ENTITY}",
        "DNS resolution failed for {ENTITY}",
        "Ping {ENTITY} timeout",
        "User logged in from {ENTITY}",
        "bind {ENTITY}",
        "The server is available at {ENTITY}",
        "Allowed origin: https://{ENTITY}",
        "Blocked connection to {ENTITY}",
        "Node {ENTITY} joined the cluster.",
        "Server responded from {ENTITY}",
        "Testing connectivity to {ENTITY}",
        "Listen on IPv6 {ENTITY}",
        "{ENTITY} is mapped to localhost in /etc/hosts",
        "Route added via {ENTITY}",
        "My IP is {ENTITY}",
        "Client IP {ENTITY} was blocked",
        "Pinging {ENTITY}...",
    ]

def get_hostname_contexts() -> List[str]:
    return [
        "Server responded from {ENTITY}",
        "Database host is {ENTITY}",
        "Testing connectivity to {ENTITY}",
        "upstream cluster: {ENTITY}",
        "Failed SSH to {ENTITY} as root.",
        "DNS resolution failed for {ENTITY}",
        "{ENTITY} is mapped to loopback in /etc/hosts",
        "localhost is mapped to {ENTITY}",
        "upstream server: {ENTITY}",
        "Allowed origin: https://{ENTITY}",
    ]

def get_name_contexts() -> List[str]:
    return [
        "Customer {ENTITY} lives at",
        "Contact {ENTITY} at",
        "Contact Dr. {ENTITY} for emergency access",
        "Send the package to {ENTITY}",
        "Call {ENTITY} at",
        "The new director, {ENTITY}, approved",
        "Hey {ENTITY}, did you review PR 42?",
        "User {ENTITY} logged in",
        "Please forward this email to {ENTITY}",
        "I spoke with {ENTITY} regarding the incident",
        "Please send the invoice to {ENTITY}",
        "The account belongs to {ENTITY}",
        "Report generated for {ENTITY}",
        "GDPR data request from {ENTITY}",
        "{ENTITY} has been assigned as the account owner",
    ]

def get_address_contexts() -> List[str]:
    return [
        "lives at {ENTITY}.",
        "Billing address: {ENTITY}",
        "Mailing to {ENTITY}.",
        "The package was delivered to {ENTITY}.",
    ]

def get_email_contexts() -> List[str]:
    return [
        "Please forward this email to {ENTITY}",
        "Contact me at {ENTITY}",
        "User email: {ENTITY}",
        "Send notifications to {ENTITY}",
        "Reset password link sent to {ENTITY}",
        "Failed login attempt from {ENTITY}",
        "Grant admin access to {ENTITY}",
        "Contact {ENTITY} for your password reset",
        "GDPR request filed by {ENTITY}",
    ]

def get_phone_contexts() -> List[str]:
    return [
        "Send an SMS to {ENTITY} for 2FA",
        "Call me at {ENTITY}",
        "Phone: {ENTITY}",
        "Contact number: {ENTITY}",
        "Mobile: {ENTITY}",
        "Reached out to {ENTITY} regarding the issue",
    ]

def get_secret_contexts() -> List[str]:
    return [
        "Authorization: Bearer {ENTITY}",
        "X-API-Key: {ENTITY}",
        "token = {ENTITY}",
        "credential = {ENTITY}",
        "password = {ENTITY}",
        "secret = {ENTITY}",
        "const AWS_KEY = '{ENTITY}';",
        "Basic auth header: Basic {ENTITY}",
        "client_secret = {ENTITY}",
        "Rotate API key {ENTITY} for user",
        "\"access_token\": \"{ENTITY}\"",
        "\"api_key\": \"{ENTITY}\"",
        "curl -H 'Authorization: Bearer {ENTITY}'",
        "curl -H 'Authorization: Token {ENTITY}'",
        "Stripe publishable key: {ENTITY}",
        "export DB_PASS='{ENTITY}'",
        "process.env.API_KEY || '{ENTITY}'",
        "const dbPassword = process.env.DB_PASS || '{ENTITY}';",
        "{ \"credentials\": \"{ENTITY}\" }",
        "{ \"auth\": { \"credentials\": \"{ENTITY}\" } }",
        "Token {ENTITY} was rotated",
        "gitlab-token: {ENTITY}",
        "X-Auth-Token: {ENTITY}",
        "OAuth token: {ENTITY}",
        "ftp://anonymous:{ENTITY}@remote-server/pub/file.txt",
        "ftp://user:{ENTITY}@remote-server/",
        "private_key = \"-----BEGIN RSA PRIVATE KEY-----\n{ENTITY}",
    ]

def get_generic_negative_contexts() -> List[str]:
    return [
        "The build completed in {ENTITY} ms",
        "Deployment id {ENTITY} failed",
        "version {ENTITY} was not found",
        "Error {ENTITY}: Page not found",
        "Trace ID {ENTITY} closed",
        "See RFC {ENTITY}",
        "timeout = {ENTITY}",
        "seed = {ENTITY}",
        "request_id = {ENTITY}",
        "build_number = {ENTITY}",
        "process_id = {ENTITY}",
        "leased IP for {ENTITY} seconds",
        "status code {ENTITY}",
        "exit code {ENTITY}",
        "The random seed is {ENTITY}",
        # Role/username negatives — teach model these are O, not PII
        "ssh {ENTITY}@remote-server",
        "mysql -u {ENTITY} -p",
        "RUN adduser --no-create-home {ENTITY}",
        "sudo -u {ENTITY} sh",
        "chmod 644 /home/{ENTITY}/.ssh",
    ]


class ContextPadder:
    """
    Wraps base contexts (e.g. `port = 8080`) into long realistic blocks
    (logs, code, JSON) to ensure the model sees diverse sequence lengths.
    """
    
    @staticmethod
    def pad_log(base_text: str, rng: random.Random) -> str:
        date = f"2026-{rng.randint(1,12):02d}-{rng.randint(1,28):02d}"
        time = f"{rng.randint(0,23):02d}:{rng.randint(0,59):02d}:{rng.randint(0,59):02d}"
        level = rng.choice(["INFO", "WARN", "ERROR", "DEBUG", "TRACE"])
        thread = f"[Thread-{rng.randint(1, 99)}]"
        logger = f"com.{_rand_str(rng, 5)}.App"
        
        prefix = f"{date} {time} [{level}] {thread} {logger} - "
        
        if rng.random() > 0.5:
            suffix = f" [duration={rng.randint(1, 5000)}ms id={_rand_str(rng, 8)}]"
        else:
            suffix = ""
            
        return prefix + base_text + suffix

    @staticmethod
    def pad_json(base_text: str, rng: random.Random) -> str:
        k1, k2 = _rand_str(rng, 6), _rand_str(rng, 8)
        v1, v2 = rng.randint(1, 100), _rand_str(rng, 10)
        
        prefix = f"{{\n  \"{k1}\": {v1},\n  \"{k2}\": \"{v2}\",\n  \"content\": \""
        suffix = "\",\n  \"active\": true\n}"
        
        # We need to make sure base_text doesn't break JSON string technically, 
        # but for NER simulation, raw text is fine. 
        # If the base text has quotes, we should be careful, but we can just use 
        # a structural wrapper that looks like JSON.
        prefix2 = f"{{\n  \"metadata\": {{\n    \"id\": \"{_rand_str(rng, 12)}\"\n  }},\n  \"data\": "
        suffix2 = "\n}"
        
        if rng.random() > 0.5:
            return prefix + base_text + suffix
        else:
            return prefix2 + base_text + suffix2

    @staticmethod
    def pad_code(base_text: str, rng: random.Random) -> str:
        func_name = rng.choice(["setup", "initialize", "connect", "startServer", "bootstrap"])
        var_name = _rand_str(rng, 6)
        
        prefix = f"function {func_name}() {{\n  const {var_name} = getContext();\n  "
        suffix = f"\n  return {var_name};\n}}"
        
        prefix2 = f"class Application {{\n  constructor() {{\n    "
        suffix2 = f"\n  }}\n}}"
        
        if rng.random() > 0.5:
            return prefix + base_text + suffix
        else:
            return prefix2 + base_text + suffix2

    @staticmethod
    def apply_random_padding_with_offset(base_text: str, rng: random.Random) -> tuple[str, int]:
        r = rng.random()
        if r < 0.3:
            return base_text, 0
        elif r < 0.5:
            date = f"2026-{rng.randint(1,12):02d}-{rng.randint(1,28):02d}"
            time = f"{rng.randint(0,23):02d}:{rng.randint(0,59):02d}:{rng.randint(0,59):02d}"
            level = rng.choice(["INFO", "WARN", "ERROR", "DEBUG", "TRACE"])
            thread = f"[Thread-{rng.randint(1, 99)}]"
            logger = f"com.{_rand_str(rng, 5)}.App"
            prefix = f"{date} {time} [{level}] {thread} {logger} - "
            suffix = f" [duration={rng.randint(1, 5000)}ms id={_rand_str(rng, 8)}]" if rng.random() > 0.5 else ""
            return prefix + base_text + suffix, len(prefix)
        elif r < 0.75:
            if rng.random() > 0.5:
                func_name = rng.choice(["setup", "initialize", "connect", "startServer", "bootstrap"])
                var_name = _rand_str(rng, 6)
                prefix = f"function {func_name}() {{\n  const {var_name} = getContext();\n  "
                suffix = f"\n  return {var_name};\n}}"
            else:
                prefix = f"class Application {{\n  constructor() {{\n    "
                suffix = f"\n  }}\n}}"
            return prefix + base_text + suffix, len(prefix)
        else:
            if rng.random() > 0.5:
                k1, k2 = _rand_str(rng, 6), _rand_str(rng, 8)
                v1, v2 = rng.randint(1, 100), _rand_str(rng, 10)
                prefix = f"{{\n  \"{k1}\": {v1},\n  \"{k2}\": \"{v2}\",\n  \"content\": \""
                suffix = "\",\n  \"active\": true\n}"
            else:
                prefix = f"{{\n  \"metadata\": {{\n    \"id\": \"{_rand_str(rng, 12)}\"\n  }},\n  \"data\": "
                suffix = "\n}"
            return prefix + base_text + suffix, len(prefix)

    @staticmethod
    def apply_random_padding(base_text: str, rng: random.Random) -> str:
        padded, _ = ContextPadder.apply_random_padding_with_offset(base_text, rng)
        return padded



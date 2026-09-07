# Entity Annotation Taxonomy

This document formalizes the explicit annotation policies for the ClipboardManager NER classifier. It is the definitive source of truth for ambiguous cases to ensure internal consistency between the dataset generator and human-annotated challenge sets.

## Category Contract

### 1. SECRET
**Rule:** Annotated spans must cover *only* the secret value itself, excluding surrounding JSON structure, variable names, or protocol markers.
- **Example:** `Authorization: Bearer <SECRET>token_abc123</SECRET>`
- **Example:** `"password": "<SECRET>P@ssw0rd</SECRET>"`
- **Negative:** Structural words like "Bearer", "Basic", "token:", "secret=" are strictly `O`.

### 2. PII (Personally Identifiable Information)
**Rule:** Encompasses names of people, full physical addresses, phone numbers, and email addresses.
- **Example:** `Send to <PII>Jane Doe</PII> at <PII>123 Main St, Springfield</PII>.`
- **Example:** `Call <PII>+1-800-555-0199</PII> or email <PII>admin@company.test</PII>.`
- **Negative:** Generic greetings (e.g., "Hello User") or organizational roles (e.g., "Admin") without a specific identifier are `O`.

### 3. HOSTINFO (Host, Domain, IP Address)
**Rule:** Encompasses fully qualified domain names, internal hostnames, IPv4 addresses, and IPv6 addresses. If a socket address (host + port) is present, only the host portion is `HOSTINFO`.
- **Example:** `<HOSTINFO>192.168.1.100</HOSTINFO>` (IPv4 is ALWAYS HOSTINFO, even if isolated).
- **Example:** `<HOSTINFO>api.internal.corp</HOSTINFO>`
- **Example:** IPv6 loopback is included: `<HOSTINFO>::1</HOSTINFO>` (IPv6 is ALWAYS HOSTINFO, even if isolated).
- **Example (Bracketed IPv6):** In `https://[<HOSTINFO>fe80::1</HOSTINFO>]:443`, the brackets `[` and `]` are strictly `O`.
- **Negative:** Subnets and CIDR blocks (e.g. `10.0.0.0/8`) are `NETWORK`, not `HOSTINFO`.

### 4. NETWORK (Ports, MAC Addresses, Subnets)
**Rule:** Encompasses bare ports, MAC addresses, and subnet ranges. 
- **Example (Bare Port):** `server.port = <NETWORK>8080</NETWORK>`
- **Example (MAC):** `MAC address is <NETWORK>02:AB:CD:EF:00:01</NETWORK>`
- **Example (Subnet):** `allow <NETWORK>192.168.0.0/16</NETWORK>`

### 5. Composite Structures (URLs, Connection Strings, Sockets)
**Rule: Strict Decomposition.** Complex strings must ALWAYS be annotated compositionally as their constituent granular entities. Do not wrap an entire URL, socket, or connection string as a single entity.
- **Socket / IP:Port:** The IP is `HOSTINFO`, the port is `NETWORK`.
  - *Correct:* `<HOSTINFO>0.0.0.0</HOSTINFO>:<NETWORK>8080</NETWORK>`
  - *Incorrect:* `<HOSTINFO>0.0.0.0:8080</HOSTINFO>`
- **HTTP/HTTPS URLs:** The domain/IP is `HOSTINFO`, explicit port is `NETWORK`. Protocols and paths are `O`.
  - *Correct:* `http://<HOSTINFO>localhost</HOSTINFO>:<NETWORK>3000</NETWORK>/api/v1/users`
- **DB Connection Strings:** Credentials decompose to `PII` and `SECRET`. Host to `HOSTINFO`. Port to `NETWORK`.
  - *Correct:* `postgres://<PII>admin</PII>:<SECRET>dbpass</SECRET>@<HOSTINFO>db.example.test</HOSTINFO>:<NETWORK>5432</NETWORK>/prod`
- **Email Addresses:** An email address is treated as a single `PII` entity. Do NOT decompose the domain of an email address into `HOSTINFO`.
  - *Correct:* `<PII>john@example.com</PII>`

### NEGATIVE (O)
**Rule:** Values that are structurally similar to entities but serve a different functional purpose must remain `O`.
- **Numbers:** Random seeds (`424242`), process IDs, thread counts, timeouts, HTTP status codes (`200`, `404`), 2FA (`2FA`).
- **Identifiers:** UUIDs (`123e4567-e89b-12d3-a456-426614174000`), request/trace IDs, Git hashes.
- **System Roles / Usernames:** System usernames in shell/config contexts (`root`, `admin`, `guest`, `user`, `postgres`, `mysql`, `www-data`, `nobody`) are strictly `O`, UNLESS they appear in a DB URI credential position (e.g. `postgres://admin:pass@host`).
- **Generic Names:** Isolated, generic first names (e.g., "Bob", "Alice") should NOT be labeled unless explicitly contextualized in a sensitive manner to avoid false positives.
- **Public/Doc Domains:** `www.example.com`, `example.org` (when used strictly as placeholders without sensitive implications, though standard synthetic data may still flag these if indistinguishable).

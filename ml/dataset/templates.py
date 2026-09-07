"""
ml/dataset/templates.py

Context template families with stable template IDs.
Each template has a stable ID, a family name, and a split assignment.

Placeholder convention:
  {SECRET}   — inject a SECRET entity value
  {PII}      — inject a PII entity value
  {HOSTINFO} — inject a HOSTINFO entity value
  {PORT}     — inject a PORT entity value (mapped to NETWORK tag)
  {MAC}      — inject a MAC entity value (mapped to NETWORK tag)
  {SUBNET}   — inject a SUBNET entity value (mapped to NETWORK tag)
  {NEG}      — inject a NEGATIVE (O-labelled) value

Split assignment strategy:
  Templates are explicitly assigned to train / val / test sets.
  The same template ID never appears in more than one split.
  Split is performed by template ID, not by row shuffle.
"""

from __future__ import annotations
from dataclasses import dataclass, field
from typing import List


@dataclass
class Template:
    template_id: str
    family: str
    split: str          # "train" | "val" | "test"
    text: str


ALL_TEMPLATES: List[Template] = [

    # -----------------------------------------------------------------------
    # FAMILY: config
    # -----------------------------------------------------------------------
    Template("config_001", "config", "train", "DB_HOST={HOSTINFO}"),
    Template("config_002", "config", "train", "DATABASE_HOST = {HOSTINFO}"),
    Template("config_003", "config", "train", "API_KEY={SECRET}"),
    Template("config_004", "config", "train", "api_key: {SECRET}"),
    Template("config_005", "config", "train", "export AWS_ACCESS_KEY_ID={SECRET}"),
    Template("config_006", "config", "train", "PORT={PORT}"),
    Template("config_007", "config", "train", "bind_address = {HOSTINFO}"),
    Template("config_008", "config", "train", "server.host={HOSTINFO} server.port={PORT}"),
    Template("config_009", "config", "train", "REDIS_HOST={HOSTINFO} REDIS_PORT={PORT}"),
    Template("config_010", "config", "train", "SECRET_KEY={SECRET} DEBUG=False"),
    Template("config_011", "config", "train", "smtp_host={HOSTINFO} smtp_port={PORT} smtp_user={PII}"),
    Template("config_012", "config", "train", "CONTACT_EMAIL={PII}"),
    Template("config_013", "config", "train", "[database] host={HOSTINFO} port={PORT}"),
    Template("config_014", "config", "train", "AUTH_TOKEN={SECRET} BASE_URL={NEG}"),
    Template("config_015", "config", "train", "CACHE_HOST={HOSTINFO}"),
    Template("config_016", "config", "val",   "jwt_secret={SECRET}"),
    Template("config_017", "config", "val",   "POSTGRES_HOST={HOSTINFO} POSTGRES_PORT={PORT}"),
    Template("config_018", "config", "val",   "oauth_client_secret={SECRET}"),
    Template("config_019", "config", "val",   "primary_host={HOSTINFO} replica_host={HOSTINFO}"),
    Template("config_020", "config", "test",  "server.address={HOSTINFO}"),
    Template("config_021", "config", "test",  "auth.token: {SECRET}"),
    Template("config_022", "config", "test",  "QUEUE_HOST={HOSTINFO} QUEUE_PORT={PORT}"),
    Template("config_023", "config", "test",  "ADMIN_EMAIL={PII} ADMIN_TOKEN={SECRET}"),

    # -----------------------------------------------------------------------
    # FAMILY: logs
    # -----------------------------------------------------------------------
    Template("logs_001", "logs", "train", "[INFO] Connecting to {HOSTINFO} on port {PORT}"),
    Template("logs_002", "logs", "train", "[ERROR] Failed to authenticate user {PII} with key {SECRET}"),
    Template("logs_003", "logs", "train", "[WARN] Timeout while contacting {HOSTINFO}"),
    Template("logs_004", "logs", "train", "Listening on {PORT} for connections from {HOSTINFO}"),
    Template("logs_005", "logs", "train", "[DEBUG] Token {SECRET} accepted for session {NEG}"),
    Template("logs_006", "logs", "train", "[INFO] User {PII} authenticated successfully"),
    Template("logs_007", "logs", "train", "[ERROR] Connection refused from {HOSTINFO}:{PORT}"),
    Template("logs_008", "logs", "train", "[INFO] Bound to {HOSTINFO} port {PORT}"),
    Template("logs_009", "logs", "train", "[AUDIT] API key {SECRET} used from {HOSTINFO}"),
    Template("logs_010", "logs", "train", "[INFO] Sending notification to {PII}"),
    Template("logs_011", "logs", "train", "[DEBUG] Subnet {SUBNET} unreachable from {HOSTINFO}"),
    Template("logs_012", "logs", "train", "Starting server on {HOSTINFO}:{PORT} at {NEG}"),
    Template("logs_013", "logs", "val",   "Connection refused from {HOSTINFO}"),
    Template("logs_014", "logs", "val",   "User {PII} logged out successfully"),
    Template("logs_015", "logs", "val",   "[WARN] Deprecated key {SECRET} still in use"),
    Template("logs_016", "logs", "test",  "[ERROR] Timeout while contacting {HOSTINFO}:{PORT}"),
    Template("logs_017", "logs", "test",  "Invalid token {SECRET} provided by {PII}"),
    Template("logs_018", "logs", "test",  "[AUDIT] Access from {HOSTINFO} on MAC {MAC} denied"),

    # -----------------------------------------------------------------------
    # FAMILY: code
    # -----------------------------------------------------------------------
    Template("code_001", "code", "train", "const host = '{HOSTINFO}';"),
    Template("code_002", "code", "train", "let apiKey = '{SECRET}';"),
    Template("code_003", "code", "train", "private string _connectionString = \"Server={HOSTINFO};Port={PORT}\";"),
    Template("code_004", "code", "train", "string token = \"{SECRET}\";"),
    Template("code_005", "code", "train", "var email = \"{PII}\";"),
    Template("code_006", "code", "train", "redisHost = \"{HOSTINFO}\"; redisPort = {PORT};"),
    Template("code_007", "code", "train", "AUTH_SECRET = os.environ.get('AUTH_SECRET', '{SECRET}')"),
    Template("code_008", "code", "train", "contact_email = '{PII}'"),
    Template("code_009", "code", "train", "PASSWORD = \"{SECRET}\"  # pragma: nocover"),
    Template("code_010", "code", "train", "DB_HOST = \"{HOSTINFO}\"\nDB_PORT = {PORT}"),
    Template("code_011", "code", "val",   "var userEmail = \"{PII}\";"),
    Template("code_012", "code", "val",   "private string token = \"{SECRET}\";"),
    Template("code_013", "code", "val",   "class Config: HOST = \"{HOSTINFO}\" PORT = {PORT}"),
    Template("code_014", "code", "test",  "app.listen({PORT}, '{HOSTINFO}');"),
    Template("code_015", "code", "test",  "def get_key(): return '{SECRET}'"),
    Template("code_016", "code", "test",  "return f\"tcp://{HOSTINFO}:{PORT}\""),

    # -----------------------------------------------------------------------
    # FAMILY: cli
    # -----------------------------------------------------------------------
    Template("cli_001", "cli", "train", "curl -H 'Authorization: Bearer {SECRET}' http://{HOSTINFO}:{PORT}"),
    Template("cli_002", "cli", "train", "ping -c 4 {HOSTINFO}"),
    Template("cli_003", "cli", "train", "ssh {PII}@{HOSTINFO}"),
    Template("cli_004", "cli", "train", "nmap -p {PORT} {HOSTINFO}"),
    Template("cli_005", "cli", "train", "redis-cli -h {HOSTINFO} -p {PORT} -a {SECRET}"),
    Template("cli_006", "cli", "train", "export API_KEY={SECRET}"),
    Template("cli_007", "cli", "train", "psql -h {HOSTINFO} -p {PORT} -U {PII}"),
    Template("cli_008", "cli", "train", "docker run -e SECRET={SECRET} -e HOST={HOSTINFO} my-app"),
    Template("cli_009", "cli", "train", "ip route add {SUBNET} via {HOSTINFO}"),
    Template("cli_010", "cli", "val",   "wget http://{HOSTINFO}:{PORT}/download"),
    Template("cli_011", "cli", "val",   "aws s3 ls --profile {NEG}"),
    Template("cli_012", "cli", "test",  "nc -zv {HOSTINFO} {PORT}"),
    Template("cli_013", "cli", "test",  "kubectl set env deployment/app API_KEY={SECRET}"),

    # -----------------------------------------------------------------------
    # FAMILY: natural_language
    # -----------------------------------------------------------------------
    Template("nl_001", "natural_language", "train", "Please email me at {PII} if the server {HOSTINFO} goes down."),
    Template("nl_002", "natural_language", "train", "The new API key is {SECRET}."),
    Template("nl_003", "natural_language", "train", "Can you check if {HOSTINFO} is reachable on port {PORT}?"),
    Template("nl_004", "natural_language", "train", "Contact {PII} for more information about the service."),
    Template("nl_005", "natural_language", "train", "The subnet {SUBNET} is currently experiencing issues."),
    Template("nl_006", "natural_language", "train", "I accidentally committed {SECRET} to the repository."),
    Template("nl_007", "natural_language", "train", "The user {PII} reported an issue with host {HOSTINFO}."),
    Template("nl_008", "natural_language", "train", "Please rotate the key {SECRET} immediately."),
    Template("nl_009", "natural_language", "train", "The server at {HOSTINFO}:{PORT} is returning errors."),
    Template("nl_010", "natural_language", "train", "My IP is {HOSTINFO} and my MAC is {MAC}."),
    Template("nl_011", "natural_language", "val",   "Contact {PII} for more information."),
    Template("nl_012", "natural_language", "val",   "Block traffic from subnet {SUBNET} at firewall {HOSTINFO}."),
    Template("nl_013", "natural_language", "test",  "Send the credentials {SECRET} to {PII} securely."),
    Template("nl_014", "natural_language", "test",  "Our support team is at {PII} and the API host is {HOSTINFO}."),

    # -----------------------------------------------------------------------
    # FAMILY: database
    # -----------------------------------------------------------------------
    Template("db_001", "database", "train", "postgres://{PII}:{SECRET}@{HOSTINFO}:{PORT}/app"),
    Template("db_002", "database", "train", "mysql://{PII}:{SECRET}@{HOSTINFO}:{PORT}/prod"),
    Template("db_003", "database", "train", "Server={HOSTINFO};Database={NEG};User Id={PII};Password={SECRET};"),
    Template("db_004", "database", "train", "mongodb://{PII}:{SECRET}@{HOSTINFO}:{PORT}/db"),
    Template("db_005", "database", "train", "GRANT ALL ON {NEG} TO {PII} IDENTIFIED BY {SECRET}"),
    Template("db_006", "database", "val",   "redis://{SECRET}@{HOSTINFO}:{PORT}"),
    Template("db_007", "database", "val",   "Data Source={HOSTINFO},{PORT};User={PII};Pwd={SECRET}"),
    Template("db_008", "database", "test",  "neo4j+s://{PII}:{SECRET}@{HOSTINFO}:{PORT}"),

    # -----------------------------------------------------------------------
    # FAMILY: environment
    # -----------------------------------------------------------------------
    Template("env_001", "environment", "train", "export DATABASE_URL=postgres://{PII}:{SECRET}@{HOSTINFO}:{PORT}/db"),
    Template("env_002", "environment", "train", "export REDIS_URL=redis://{SECRET}@{HOSTINFO}:{PORT}"),
    Template("env_003", "environment", "train", "NOTIFICATION_EMAIL={PII}"),
    Template("env_004", "environment", "train", "INTERNAL_HOST={HOSTINFO}"),
    Template("env_005", "environment", "train", "SERVICE_TOKEN={SECRET}"),
    Template("env_006", "environment", "val",   "SMTP_HOST={HOSTINFO} SMTP_PORT={PORT} SMTP_PASSWORD={SECRET}"),
    Template("env_007", "environment", "test",  "OPERATOR_EMAIL={PII} MASTER_KEY={SECRET}"),

    # -----------------------------------------------------------------------
    # FAMILY: json
    # -----------------------------------------------------------------------
    Template("json_001", "json", "train", '{"host": "{HOSTINFO}", "port": "{PORT}"}'),
    Template("json_002", "json", "train", '{"api_key": "{SECRET}", "endpoint": "{HOSTINFO}"}'),
    Template("json_003", "json", "train", '{"user": "{PII}", "token": "{SECRET}"}'),
    Template("json_004", "json", "train", '{"db_host": "{HOSTINFO}", "db_port": "{PORT}", "db_pass": "{SECRET}"}'),
    Template("json_005", "json", "val",   '{"contact": "{PII}", "server": "{HOSTINFO}"}'),
    Template("json_006", "json", "test",  '{"secret": "{SECRET}", "host": "{HOSTINFO}", "port": "{PORT}"}'),

    # -----------------------------------------------------------------------
    # FAMILY: yaml
    # -----------------------------------------------------------------------
    Template("yaml_001", "yaml", "train", "host: {HOSTINFO}\nport: {PORT}"),
    Template("yaml_002", "yaml", "train", "secret_key: {SECRET}\ndebug: false"),
    Template("yaml_003", "yaml", "train", "database:\n  host: {HOSTINFO}\n  password: {SECRET}"),
    Template("yaml_004", "yaml", "train", "smtp:\n  host: {HOSTINFO}\n  port: {PORT}\n  user: {PII}"),
    Template("yaml_005", "yaml", "val",   "credentials:\n  token: {SECRET}"),
    Template("yaml_006", "yaml", "test",  "server:\n  address: {HOSTINFO}\n  port: {PORT}\n  key: {SECRET}"),

    # -----------------------------------------------------------------------
    # FAMILY: diagnostic
    # -----------------------------------------------------------------------
    Template("diag_001", "diagnostic", "train", "Health check failed for {HOSTINFO}:{PORT} — verify key {SECRET}"),
    Template("diag_002", "diagnostic", "train", "Alert: certificate mismatch on {HOSTINFO}"),
    Template("diag_003", "diagnostic", "train", "Network {SUBNET} unreachable; contact {PII}"),
    Template("diag_004", "diagnostic", "train", "Rotating secret {SECRET} for service at {HOSTINFO}"),
    Template("diag_005", "diagnostic", "val",   "Firewall rule rejected {SUBNET} from {HOSTINFO}"),
    Template("diag_006", "diagnostic", "test",  "CRITICAL: leaked credential {SECRET} in repo — notify {PII}"),

    # -----------------------------------------------------------------------
    # FAMILY: negative  — all entities are O-class
    # Templates that look similar to positive cases but contain no entities
    # -----------------------------------------------------------------------
    Template("neg_001", "negative", "train", "The process finished in 45ms."),
    Template("neg_002", "negative", "train", "Starting application version {NEG}"),
    Template("neg_003", "negative", "train", "Error: File not found {NEG}"),
    Template("neg_004", "negative", "train", "Request ID: {NEG} completed in 12ms"),
    Template("neg_005", "negative", "train", "Build {NEG} deployed successfully at {NEG}"),
    Template("neg_006", "negative", "train", "Loading configuration from {NEG}"),
    Template("neg_007", "negative", "train", "Timeout after {NEG} seconds"),
    Template("neg_008", "negative", "train", "Retrying after {NEG} milliseconds"),
    Template("neg_009", "negative", "train", "Session {NEG} established, duration {NEG}"),
    Template("neg_010", "negative", "train", "Processed {NEG} records in batch {NEG}"),
    Template("neg_011", "negative", "train", "Worker {NEG} completed task {NEG}"),
    Template("neg_012", "negative", "train", "Cache hit ratio: {NEG}"),
    Template("neg_013", "negative", "val",   "Operation completed successfully."),
    Template("neg_014", "negative", "val",   "Loading configuration from {NEG}."),
    Template("neg_015", "negative", "val",   "Service {NEG} is healthy."),
    Template("neg_016", "negative", "test",  "Debug: {NEG}"),
    Template("neg_017", "negative", "test",  "Shutting down gracefully at {NEG}"),
    Template("neg_018", "negative", "test",  "Trace {NEG} closed after {NEG} steps"),
    
    # =======================================================================
    # v1.1 ADDITIONS — longer context, multi-entity, hard negatives
    # IDs prefixed with x_ to distinguish from v1.0 templates
    # =======================================================================

    # -----------------------------------------------------------------------
    # FAMILY: long_config  — multi-line, 30–80 token contexts
    # -----------------------------------------------------------------------
    Template("xconf_001", "long_config", "train",
             "# Application Settings\nDB_HOST={HOSTINFO}\nDB_PORT={PORT}\nDB_USER={PII}\nDB_PASS={SECRET}\nDEBUG=false"),
    Template("xconf_002", "long_config", "train",
             "[redis]\nhost = {HOSTINFO}\nport = {PORT}\npassword = {SECRET}\ntimeout = 30"),
    Template("xconf_003", "long_config", "train",
             "services:\n  api:\n    host: {HOSTINFO}\n    port: {PORT}\n    token: {SECRET}\n    admin: {PII}"),
    Template("xconf_004", "long_config", "train",
             "CACHE_HOST={HOSTINFO} CACHE_PORT={PORT} CACHE_PASS={SECRET} CACHE_TTL=300"),
    Template("xconf_005", "long_config", "train",
             "# SMTP\nSMTP_HOST={HOSTINFO}\nSMTP_PORT={PORT}\nSMTP_USER={PII}\nSMTP_PASS={SECRET}"),
    Template("xconf_006", "long_config", "train",
             "[credentials]\naccess_key = {SECRET}\nsecret_key = {SECRET}\nregion = us-east-1"),
    Template("xconf_007", "long_config", "val",
             "DATABASE_URL=postgres://{PII}:{SECRET}@{HOSTINFO}:{PORT}/production\nREDIS_URL=redis://:{SECRET}@{HOSTINFO}:{PORT}"),
    Template("xconf_008", "long_config", "val",
             "broker:\n  host: {HOSTINFO}\n  port: {PORT}\n  vhost: /\n  user: {PII}\n  password: {SECRET}"),
    Template("xconf_009", "long_config", "test",
             "# Firewall rules\nallow_from = {SUBNET}\nblocked_host = {HOSTINFO}\napi_key = {SECRET}"),
    Template("xconf_010", "long_config", "test",
             "MONITOR_HOST={HOSTINFO} MONITOR_PORT={PORT} ALERT_EMAIL={PII} API_SECRET={SECRET}"),

    # -----------------------------------------------------------------------
    # FAMILY: long_logs  — realistic multi-statement log entries
    # -----------------------------------------------------------------------
    Template("xlogs_001", "long_logs", "train",
             "2026-09-01T08:00:00Z [ERROR] Failed login for user {PII} from {HOSTINFO} port {PORT} using key {SECRET}"),
    Template("xlogs_002", "long_logs", "train",
             "2026-09-01T08:01:00Z [AUDIT] Token {SECRET} issued to {PII} for host {HOSTINFO} on port {PORT}"),
    Template("xlogs_003", "long_logs", "train",
             "[WARN] Retrying connection to {HOSTINFO}:{PORT} — attempt 3 of 5 — using credential {SECRET}"),
    Template("xlogs_004", "long_logs", "train",
             "[INFO] Health check from {HOSTINFO} on port {PORT} — status OK — notifying {PII}"),
    Template("xlogs_005", "long_logs", "train",
             "[SECURITY] Blocked subnet {SUBNET} — {NEG} requests from {HOSTINFO} — contact {PII}"),
    Template("xlogs_006", "long_logs", "train",
             "2026-09-01T09:00:00Z [ERROR] DB connection to {HOSTINFO}:{PORT} failed — password {SECRET} rejected"),
    Template("xlogs_007", "long_logs", "val",
             "[AUDIT] Service account {PII} rotated key {SECRET} — old key deactivated — new host {HOSTINFO}"),
    Template("xlogs_008", "long_logs", "val",
             "[INFO] Route {SUBNET} added via gateway {HOSTINFO} — operator {PII}"),
    Template("xlogs_009", "long_logs", "test",
             "[ERROR] Certificate verification failed for {HOSTINFO}:{PORT} — notify admin {PII} — token {SECRET}"),
    Template("xlogs_010", "long_logs", "test",
             "2026-09-01T10:00:00Z [DEBUG] Sent payload to {HOSTINFO} on port {PORT} — auth Bearer {SECRET}"),

    # -----------------------------------------------------------------------
    # FAMILY: long_code  — realistic multi-line code blocks
    # -----------------------------------------------------------------------
    Template("xcode_001", "long_code", "train",
             "const config = { host: '{HOSTINFO}', port: {PORT}, token: '{SECRET}', admin: '{PII}' };"),
    Template("xcode_002", "long_code", "train",
             "client = redis.Redis(host='{HOSTINFO}', port={PORT}, password='{SECRET}', decode_responses=True)"),
    Template("xcode_003", "long_code", "train",
             "conn = psycopg2.connect(host='{HOSTINFO}', port={PORT}, user='{PII}', password='{SECRET}', dbname='{NEG}')"),
    Template("xcode_004", "long_code", "train",
             "server.listen({ host: '{HOSTINFO}', port: {PORT} }); console.log('listening');"),
    Template("xcode_005", "long_code", "train",
             "headers = {'Authorization': f'Bearer {SECRET}', 'X-Host': '{HOSTINFO}'}\nresponse = requests.get(f'http://{HOSTINFO}:{PORT}/api', headers=headers)"),
    Template("xcode_006", "long_code", "train",
             "smtp = smtplib.SMTP('{HOSTINFO}', {PORT})\nsmtp.login('{PII}', '{SECRET}')"),
    Template("xcode_007", "long_code", "val",
             "MongoClient(f'mongodb://{PII}:{SECRET}@{HOSTINFO}:{PORT}/{NEG}?authSource=admin')"),
    Template("xcode_008", "long_code", "val",
             "config.set('redis.host', '{HOSTINFO}').set('redis.port', {PORT}).set('redis.pass', '{SECRET}')"),
    Template("xcode_009", "long_code", "test",
             "curl.setopt(pycurl.URL, f'https://{HOSTINFO}:{PORT}/data')\ncurl.setopt(pycurl.HTTPHEADER, [f'Authorization: {SECRET}'])"),
    Template("xcode_010", "long_code", "test",
             "app.config.update(SECRET_KEY='{SECRET}', SERVER_NAME='{HOSTINFO}:{PORT}', ADMIN_EMAIL='{PII}')"),

    # -----------------------------------------------------------------------
    # FAMILY: multi_entity  — sentences with 3+ distinct entity types
    # -----------------------------------------------------------------------
    Template("xme_001", "multi_entity", "train",
             "Alert: {PII} detected unauthorized access using key {SECRET} from {HOSTINFO} on port {PORT}"),
    Template("xme_002", "multi_entity", "train",
             "Contact {PII} to rotate {SECRET} and update firewall rules for {HOSTINFO} subnet {SUBNET}"),
    Template("xme_003", "multi_entity", "train",
             "The service at {HOSTINFO}:{PORT} accepted token {SECRET} for user {PII}"),
    Template("xme_004", "multi_entity", "train",
             "ssh -p {PORT} {PII}@{HOSTINFO} # use key {SECRET}"),
    Template("xme_005", "multi_entity", "train",
             "{PII} committed {SECRET} to main — host {HOSTINFO} must rotate credentials — block {SUBNET}"),
    Template("xme_006", "multi_entity", "train",
             "Backup DB from {HOSTINFO}:{PORT} — auth as {PII} with {SECRET} — store locally"),
    Template("xme_007", "multi_entity", "val",
             "Security event: credential {SECRET} exposed by {PII} on host {HOSTINFO} port {PORT}"),
    Template("xme_008", "multi_entity", "val",
             "Rotate API key {SECRET} — notify {PII} — update {HOSTINFO}:{PORT} config"),
    Template("xme_009", "multi_entity", "test",
             "MAC {MAC} registered to {PII} on segment {HOSTINFO} — token {SECRET} valid for 24h"),
    Template("xme_010", "multi_entity", "test",
             "Deploy to {HOSTINFO}:{PORT} as user {PII} using deploy key {SECRET}"),

    # -----------------------------------------------------------------------
    # FAMILY: hard_negative  — values that look like entities but are O
    # Teaches model: structural similarity ≠ entity
    # -----------------------------------------------------------------------
    Template("xneg_001", "hard_negative", "train",
             "Request {NEG} completed in 45ms — status 200"),
    Template("xneg_002", "hard_negative", "train",
             "Build pipeline {NEG} passed all checks — version {NEG} promoted"),
    Template("xneg_003", "hard_negative", "train",
             "Session ID {NEG} expired after {NEG} seconds of inactivity"),
    Template("xneg_004", "hard_negative", "train",
             "Trace ID {NEG} closed — total steps {NEG} — CPU: {NEG}ms"),
    Template("xneg_005", "hard_negative", "train",
             "Worker {NEG} finished job {NEG} — throughput: {NEG} req/s"),
    Template("xneg_006", "hard_negative", "train",
             "Cache miss for key {NEG} — fetched from DB — latency {NEG}ms"),
    Template("xneg_007", "hard_negative", "train",
             "Config loaded from {NEG} — {NEG} keys applied — {NEG} warnings"),
    Template("xneg_008", "hard_negative", "train",
             "Health check at {NEG} — {NEG} services online — {NEG} degraded"),
    Template("xneg_009", "hard_negative", "train",
             "Published version {NEG} to registry {NEG} — {NEG} consumers notified"),
    Template("xneg_010", "hard_negative", "train",
             "Rollback to {NEG} initiated — ETA {NEG} minutes — operator notified"),
    Template("xneg_011", "hard_negative", "val",
             "Deployment {NEG} completed — {NEG} pods running — {NEG} pending"),
    Template("xneg_012", "hard_negative", "val",
             "GC pause {NEG}ms — heap used {NEG}MB — threads {NEG}"),
    Template("xneg_013", "hard_negative", "test",
             "Retry {NEG} of {NEG} for job {NEG} — timeout in {NEG}s"),
    Template("xneg_014", "hard_negative", "test",
             "API version {NEG} deprecated — migrate to {NEG} by {NEG}"),

    # -----------------------------------------------------------------------
    # FAMILY: connection_strings  — full realistic DSN/URI blocks
    # -----------------------------------------------------------------------
    Template("xconn_001", "connection_strings", "train",
             "Server={HOSTINFO},{PORT};Initial Catalog={NEG};User ID={PII};Password={SECRET};Encrypt=True"),
    Template("xconn_002", "connection_strings", "train",
             "amqp://{PII}:{SECRET}@{HOSTINFO}:{PORT}/production"),
    Template("xconn_003", "connection_strings", "train",
             "cassandra://{PII}:{SECRET}@{HOSTINFO}:{PORT}/{NEG}"),
    Template("xconn_004", "connection_strings", "train",
             "elasticsearch+https://{PII}:{SECRET}@{HOSTINFO}:{PORT}"),
    Template("xconn_005", "connection_strings", "val",
             "jdbc:postgresql://{HOSTINFO}:{PORT}/{NEG}?user={PII}&password={SECRET}&sslmode=require"),
    Template("xconn_006", "connection_strings", "test",
             "Driver={NEG};Server={HOSTINFO};Port={PORT};UID={PII};PWD={SECRET}"),

    # =======================================================================
    # v1.2 ADDITIONS — targeted failure mode generalization
    # =======================================================================

    # -----------------------------------------------------------------------
    # FAMILY: prose_pii  — natural language names and physical addresses
    # -----------------------------------------------------------------------
    Template("v12_pii_001", "prose_pii", "train", "The delivery for {PII} was sent to {PII} yesterday."),
    Template("v12_pii_002", "prose_pii", "train", "Please forward the invoice to {PII}."),
    Template("v12_pii_003", "prose_pii", "train", "Customer {PII} lives at {PII} and can be reached via email."),
    Template("v12_pii_004", "prose_pii", "train", "Contact {PII} immediately at {PII} regarding the breach."),
    Template("v12_pii_005", "prose_pii", "train", "User {PII} changed their billing address to {PII}."),
    Template("v12_pii_006", "prose_pii", "train", "Hello {PII}, your appointment is scheduled."),
    Template("v12_pii_007", "prose_pii", "train", "The director, {PII}, approved the merge request."),
    Template("v12_pii_008", "prose_pii", "val",   "Mailing address updated to {PII} for {PII}."),
    Template("v12_pii_009", "prose_pii", "val",   "Send a text to {PII} to verify the account for {PII}."),
    Template("v12_pii_010", "prose_pii", "test",  "{PII} signed the document at {PII}."),

    # -----------------------------------------------------------------------
    # FAMILY: code_ports  — bare integer ports in code structures
    # -----------------------------------------------------------------------
    Template("v12_port_001", "code_ports", "train", "port = {PORT}"),
    Template("v12_port_002", "code_ports", "train", "serverPort: {PORT},"),
    Template("v12_port_003", "code_ports", "train", "const PORT = {PORT};"),
    Template("v12_port_004", "code_ports", "train", ".listen({PORT})"),
    Template("v12_port_005", "code_ports", "train", "\"port\": {PORT},"),
    Template("v12_port_006", "code_ports", "train", "bind {HOSTINFO} {PORT}"),
    Template("v12_port_007", "code_ports", "train", "EXPOSE {PORT}"),
    Template("v12_port_008", "code_ports", "val",   "config.set('port', {PORT})"),
    Template("v12_port_009", "code_ports", "val",   "app.listen(port={PORT}, host='{HOSTINFO}')"),
    Template("v12_port_010", "code_ports", "test",  "{{ \"targetPort\": {PORT} }}"),

    # -----------------------------------------------------------------------
    # FAMILY: composite_network  — explicit HOSTINFO:PORT socket structures
    # -----------------------------------------------------------------------
    Template("v12_net_001", "composite_network", "train", "Binding server to {HOSTINFO}:{PORT} for incoming requests."),
    Template("v12_net_002", "composite_network", "train", "Health check failed for {HOSTINFO}:{PORT}."),
    Template("v12_net_003", "composite_network", "train", "Blocked connection to {HOSTINFO}:{PORT}."),
    Template("v12_net_004", "composite_network", "train", "The API is running at {HOSTINFO}:{PORT}."),
    Template("v12_net_005", "composite_network", "train", "Forwarding to {HOSTINFO}:{PORT}"),
    Template("v12_net_006", "composite_network", "train", "Proxy pass: http://{HOSTINFO}:{PORT}"),
    Template("v12_net_007", "composite_network", "train", "grpc://{HOSTINFO}:{PORT}"),
    Template("v12_net_008", "composite_network", "val",   "upstream cluster: {HOSTINFO}:{PORT}"),
    Template("v12_net_009", "composite_network", "val",   "Database host is {HOSTINFO}:{PORT}"),
    Template("v12_net_010", "composite_network", "test",  "Node IP {HOSTINFO} mapped to host port {PORT}"),

    # -----------------------------------------------------------------------
    # FAMILY: expanded_negatives  — more integers and numeric values
    # -----------------------------------------------------------------------
    Template("v12_neg_001", "expanded_negatives", "train", "seed = {NEG}"),
    Template("v12_neg_002", "expanded_negatives", "train", "request_id = {NEG}"),
    Template("v12_neg_003", "expanded_negatives", "train", "build_number = {NEG}"),
    Template("v12_neg_004", "expanded_negatives", "train", "process_id = {NEG}"),
    Template("v12_neg_005", "expanded_negatives", "train", "version = {NEG}"),
    Template("v12_neg_006", "expanded_negatives", "train", "timeout = {NEG}"),
    Template("v12_neg_007", "expanded_negatives", "train", "Error {NEG}: Page not found"),
    Template("v12_neg_008", "expanded_negatives", "val",   "The random seed is {NEG} and instance index is {NEG}."),
    Template("v12_neg_009", "expanded_negatives", "val",   "See RFC {NEG} at {NEG}"),
    Template("v12_neg_010", "expanded_negatives", "test",  "Deployment id {NEG} failed because version {NEG} was not found."),
]

# Group by split for easy lookup
def templates_by_split(split: str) -> List[Template]:
    return [t for t in ALL_TEMPLATES if t.split == split]

def templates_by_family(family: str) -> List[Template]:
    return [t for t in ALL_TEMPLATES if t.family == family]

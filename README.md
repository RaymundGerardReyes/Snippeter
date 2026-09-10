# Snippeter (ClipboardManager)

[![Version](https://img.shields.io/badge/version-1.2.1-blue.svg)](https://github.com/RaymundGerardReyes/Snippeter/releases/tag/v1.2.1)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%20Windows%2011-0078D6.svg)](https://www.microsoft.com/windows)
[![Framework](https://img.shields.io/badge/.NET-8.0%20WinUI%203-512BD4.svg)](https://dotnet.microsoft.com/)
[![Windows App SDK](https://img.shields.io/badge/Windows%20App%20SDK-1.5-0078D4.svg)](https://learn.microsoft.com/windows/apps/windows-app-sdk/)
[![DirectML](https://img.shields.io/badge/Hardware%20Acceleration-DirectML%20%2F%20ONNX-FF8000.svg)](https://github.com/microsoft/DirectML)
[![Unit Tests](https://img.shields.io/badge/tests-234%20passed%20%28100%25%29-brightgreen.svg)]()
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

**Snippeter** is a modern, high-performance, privacy-first clipboard manager engineered for Windows 10 and 11. Built with **WinUI 3 (Windows App SDK)** and **.NET 8**, Snippeter guarantees that sensitive developer credentials, secrets, tokens, IP addresses, and personally identifiable information (PII) never leak into unencrypted clipboard history or downstream apps.

---

## Key Highlights

- 🛡️ **Double-Layer Privacy & Masking Engine**: Combines deterministic high-speed regex/heuristics (Layer 1) with an on-device fine-tuned transformer model via ONNX Runtime & DirectML (Layer 2) for deep secret detection.
- ⚡ **Instant 0ms Mouse Wheel Scrolling**: Purpose-built centralized event routing and direct viewport manipulation (`disableAnimation: true`) that completely eliminates the 2000ms DirectManipulation watchdog lockout.
- 🎛️ **Master Privacy Switch & Zero-Masking Guarantee**: Global privacy toggle and granular category controls (Passwords, Secrets/Tokens, Emails, Phones, IPs, Domains). Untoggling categories provides a 100% raw text pass-through guarantee.
- 🔍 **SQLite FTS5 Full-Text Search**: Embedded SQLite database with an FTS5 virtual table (`unicode61` tokenizer) and prefix search queries for instant history lookup.
- ⌨️ **Global Hotkey Integration**: Native Win32 `Ctrl+Shift+V` hotkey registration with synthetic paste dispatch directly into target applications.
- 🔄 **Anti-Reentrancy Guard**: Dedicated `ClipboardReentrancyTracker` prevents infinite capture loops when replacing masked content on the OS clipboard.
- ⏱️ **Automatic Background TTL Sweeper**: Cleans up expired unpinned clipboard records every 60 seconds.
- 🩺 **Self-Healing Schema & Thread-Safe Logging**: Automatic database migration with idempotent table creation (`privacy_settings`) and unified diagnostic file logging (`crash.log`).

---

## System Architecture

```mermaid
flowchart TD
    OS[Windows OS Clipboard] -->|WM_CLIPBOARDUPDATE| Mon[ClipboardMonitor]
    Mon --> Tracker{Reentrancy Tracker\nExpected Write?}
    Tracker -->|Yes| Drop[Consume & Ignore]
    Tracker -->|No| Ingest[ClipboardIngestor]
    
    subgraph Privacy_Engine ["Privacy Engine"]
        Ingest --> Settings[PrivacyMaskingSettingsProvider]
        Settings --> MasterCheck{Master Switch\nEnabled?}
        MasterCheck -->|No| PassThrough[Raw Pass-Through]
        MasterCheck -->|Yes| Class[PrivacyClassifier]
        
        Class --> L1[Layer 1: Deterministic Pattern Classifier\nPasswords, Tokens, IPs, URLs, PII]
        Class --> L2[Layer 2: ML Secret Detector\nBERT Tokenizer + ONNX Runtime DirectML]
        
        L1 --> Merge[Findings Aggregator & Conflict Resolver]
        L2 --> Merge
        Merge --> Mask[MaskingService\nRedaction & Structural Masking]
    end
    
    Mask --> RepOS[ClipboardWriter\nReplace OS Clipboard]
    RepOS -.->|Register Hash| Tracker
    Mask --> Repo[ClipboardRepository\nSQLite + FTS5 Search Index]
    PassThrough --> Repo
    
    Repo --> UI[WinUI 3 MainWindow\nHistory ListView + SearchBox + Settings]
    UI -->|Click Item / Hotkey| Paste[PasteCoordinator\nSynthetic Win32 Ctrl+V Dispatch]
```

---

## Privacy Engine Architecture

Snippeter employs a double-layer defense-in-depth classification and masking pipeline:

### Layer 1 — Deterministic Heuristic Engine
Operates with sub-millisecond execution over synchronous ingestion pipelines:
- **Authentication & Secrets**: AWS keys, GitHub PATs, Slack tokens, Stripe keys, Bearer tokens, private keys (`BEGIN RSA PRIVATE KEY`), JWT tokens, password assignments (`password = ...`).
- **Database & URLs**: Database connection strings (`Server=...;Pwd=...`), Redis URIs, MongoDB URIs, JDBC endpoints, embedded URL credentials (`http://user:pass@host`).
- **Network Identifiers**: IPv4 addresses, IPv6 addresses, CIDR network ranges (`10.0.0.0/8`, `192.168.0.0/16`), MAC addresses, domain names, sensitive hostnames, port numbers.
- **Personal Identifiable Information (PII)**: Emails, international and domestic phone numbers, UUIDs/GUIDs.
- **Environment & Config Secrets**: `.env` configurations, YAML/JSON secret fields, command-line arguments (`--token`, `-p`).

### Layer 2 — Deep ML Secret Detection (DirectML + ONNX)
For ambiguous high-entropy strings and credentials in mixed prose:
- **Architecture**: Fine-tuned BERT-based token classifier exported to ONNX (Opset 17, ONNX IR 8).
- **Label Schema**: BIO token classification (`B-SECRET`, `I-SECRET`, `B-PII`, `I-PII`, `B-HOSTINFO`, `I-HOSTINFO`, `B-NETWORK`, `I-NETWORK`).
- **Performance**: Evaluated at **96.25% Span F1** and **98.97% Recall** on synthetic and real-world challenge sets.
- **Hardware Acceleration**: Powered by `Microsoft.ML.OnnxRuntime.DirectML` for GPU acceleration across any DirectX 12 hardware, with automatic fallback to CPU.

#### Machine Learning Model & Inference Graph Pipeline

```mermaid
flowchart TD
    subgraph Input_Stage ["1. Ingestion & Sliding Window Partitioning"]
        RawText["Raw Clipboard Text\n(e.g., 'connect to db_password=SuperSecret123! on node.internal:5432')"]
        Chunker["Sliding Window Chunker\nWindow: 256 tokens | Overlap: 48 tokens"]
        RawText --> Chunker
    end

    subgraph Tokenization_Stage ["2. WordPiece Tokenization & Tensor Assembly"]
        Tokenizer["BertTokenizerService\nWordPiece Vocabulary (vocab.txt)"]
        InputIds["input_ids Tensor\nint64[batch_size, 256]"]
        AttnMask["attention_mask Tensor\nint64[batch_size, 256]"]
        Offsets["Token Character Offsets\nArray of (CharStart, CharLength)"]
        
        Chunker --> Tokenizer
        Tokenizer --> InputIds
        Tokenizer --> AttnMask
        Tokenizer --> Offsets
    end

    subgraph Runtime_EP ["3. Hardware Execution Provider Routing"]
        EPCheck{DirectX 12 GPU\nAvailable?}
        DML["DirectML Execution Provider\nHardware GPU Acceleration (D3D12)"]
        CPU["CPU Execution Provider\nNative x64 / ARM64 Fallback"]
        
        InputIds & AttnMask --> EPCheck
        EPCheck -->|Yes| DML
        EPCheck -->|No / Error| CPU
    end

    subgraph ONNX_Graph ["4. ONNX Computational Graph (Opset 17, IR 8)"]
        Emb["Embedding Layer\n(Word Token + Position + Segment Embeddings)"]
        Encoders["BERT Transformer Encoder Stack\n(12 Blocks with Multi-Head Self-Attention & GELU)"]
        HiddenStates["Hidden States Representation\nfloat32[batch_size, 256, 768]"]
        ClassifierHead["Token Classification Head\nDropout + Dense Linear(768 -> 9)"]
        Logits["Logits Tensor Output\nfloat32[batch_size, 256, 9]"]
        
        DML & CPU --> Emb
        Emb --> Encoders
        Encoders --> HiddenStates
        HiddenStates --> ClassifierHead
        ClassifierHead --> Logits
    end

    subgraph Decoding_Stage ["5. BIO Span Decoding & Probabilistic Filtering"]
        Softmax["Softmax Normalization\nP(c) = exp(z_c) / Σ exp(z_k)"]
        Argmax["Argmax & BIO Label Resolver\nO, B-SECRET, I-SECRET, B-PII, I-PII,\nB-HOSTINFO, I-HOSTINFO, B-NETWORK, I-NETWORK"]
        BioDecoder["BioSpanDecoder\nToken Merging & Offset Reconstruction"]
        Dedupe["Window Overlap Resolver\nDeduplicates Spans (Prefers Highest Confidence)"]
        Threshold{"Confidence Gate\nScore >= MlConfidenceThreshold (0.75)"}
        
        Logits --> Softmax
        Softmax --> Argmax
        Argmax & Offsets --> BioDecoder
        BioDecoder --> Dedupe
        Dedupe --> Threshold
    end

    subgraph Output_Stage ["6. Extracted Privacy Findings"]
        Findings["PrivacyFinding Entity List:\n• Category: SECRET, PII, HOSTINFO, NETWORK\n• Exact Character Boundaries: StartIndex, Length\n• Confidence Score (e.g. 0.9998)\n• Severity: Critical / High / Medium"]
        Threshold -->|Pass| Findings
        Threshold -->|Fail| Discard[Discard Sub-threshold Candidate]
    end
```

---

## User Interface & Mouse Wheel Responsiveness

In Windows WinUI 3 applications, mouse wheel events on nested `ScrollViewer` elements can trigger conflicts with the Windows DirectManipulation compositor, leading to an internal **2000ms watchdog freeze**.

Snippeter resolves this at the architectural level:
1. **Centralized Event Routing**: Only the root window container (`RootGrid`) listens to `PointerWheelChanged` with `handledEventsToo: true`. Nested child elements do not attach competing handlers.
2. **Immediate Frame Dispatch**: Viewport updates use `ScrollViewer.ChangeView(..., disableAnimation: true)`. The viewport moves on the exact frame of the wheel notch with **0ms animation latency**.
3. **Dynamic Notch Accumulator**: Rapid wheel rotations accumulate within a high-speed buffer that automatically synchronizes with manual scrollbar thumb dragging.

---

## Storage & Database Design

All data is stored locally in an embedded SQLite database at:
`%LOCALAPPDATA%\ClipboardManager\clipboard_history.db`

### Schema Architecture
```sql
-- Core Clipboard Items Table
CREATE TABLE IF NOT EXISTS clipboard_items (
    id TEXT PRIMARY KEY,
    windows_id TEXT NULL,
    created_at TEXT NOT NULL,
    content_type TEXT NOT NULL,
    protection_state TEXT NOT NULL,  -- 'Normal', 'Protected', 'Expired'
    safe_text TEXT,                  -- Authoritative safe preview/text
    primary_category TEXT NOT NULL,  -- 'Secret', 'Password', 'Network', 'Normal'
    expires_at TEXT NULL,            -- TTL Expiration Timestamp
    is_pinned INTEGER NOT NULL DEFAULT 0
);

-- Full-Text Search Virtual Table (FTS5)
CREATE VIRTUAL TABLE IF NOT EXISTS clipboard_fts 
USING fts5(
    item_id UNINDEXED,
    search_text,
    tokenize='unicode61'
);

-- Persistent Settings Key-Value Store
CREATE TABLE IF NOT EXISTS privacy_settings (
    key TEXT PRIMARY KEY,
    json_value TEXT NOT NULL
);
```

---

## System Requirements

| Component | Minimum Requirement | Recommended |
| :--- | :--- | :--- |
| **Operating System** | Windows 10 Version 1809 (Build 17763) | Windows 11 (Latest) |
| **Architecture** | x64, ARM64 | x64 |
| **.NET SDK** (for build) | .NET 8.0 SDK (8.0.400+) | .NET 8.0 SDK |
| **Windows App SDK** | Version 1.5.240311000 | Version 1.5+ |
| **GPU (DirectML)** | DirectX 12 compatible GPU (Feature Level 11.0) | Dedicated NVIDIA / AMD / Intel GPU |
| **Memory (RAM)** | 4 GB | 8 GB+ |

---

## Project Structure

```text
ClipboardManager/
├── App.xaml / App.xaml.cs            # Application lifecycle, startup sequencing & thread-safe logging
├── Program.cs                        # Application entry point, COM wrappers & WinUI 3 bootstrap
├── ClipboardManager.csproj           # Standalone unpackaged project configuration & PRI build targets
├── app.manifest                      # Win32 DPI awareness & OS compatibility manifest
│
├── Data/                             # Persistence & Data Access
│   ├── ClipboardRepository.cs        # SQLite queries, CRUD & FTS5 full-text search
│   ├── DatabaseInitializer.cs       # Schema migrations & idempotent table guarantees
│   ├── SettingsRepository.cs         # Self-healing JSON settings store for PrivacyMaskingSettings
│   └── schema.sql                    # Authoritative SQLite database schema
│
├── Helpers/                          # Native Win32 & MVVM Utilities
│   ├── NativeMethods.cs              # User32/Comctl32 P/Invoke, Hotkeys, Subclassing, SendInput
│   ├── ObservableObject.cs           # INotifyPropertyChanged base implementation
│   └── RelayCommand.cs               # ICommand implementation
│
├── Models/                           # Data Models
│   ├── ClipboardItem.cs              # UI-bound clipboard history item
│   ├── ClipboardRecord.cs            # Ingested entity record
│   ├── ClipboardPayload.cs           # Raw clipboard snapshot data
│   ├── PrivacyMaskingSettings.cs     # Privacy configuration flags & threshold models
│   └── ml/                           # ML manifest & status records
│
├── Search/                           # Search Logic
│   └── SearchQueryParser.cs          # Sanitizes raw queries into safe FTS5 prefix expressions
│
├── Services/                         # Core Business Logic & Ingestion Pipeline
│   ├── ClipboardIngestor.cs          # Orchestrates classification, masking, and storage
│   ├── ClipboardMonitor.cs           # Listens for OS clipboard updates
│   ├── PrivacyClassifier.cs          # Double-layer secret & PII detection engine
│   ├── MaskingService.cs             # Masking replacement & redaction strategies
│   ├── WindowsClipboardSystem.cs     # OS clipboard read/write/history access
│   ├── ExpirationCleanupService.cs   # Background timer sweeper for expired records
│   ├── PrivacyMaskingSettingsProvider.cs # Thread-safe in-memory settings cache with async persistence
│   └── ML/                           # Machine Learning Runtime
│       ├── BertTokenizerService.cs   # WordPiece tokenization for BERT input
│       ├── BioSpanDecoder.cs         # Decodes token logits into character span findings
│       ├── MlModelLoader.cs          # Validates ONNX hashes, manifests, and version pointers
│       ├── MlSecretDetector.cs       # High-level ML inference orchestrator
│       └── OnnxInferenceRunner.cs    # ONNX Runtime execution provider (DirectML / CPU)
│
├── ViewModels/                       # Presentation Logic
│   ├── ClipboardViewModel.cs         # History list binding, search, pin/delete commands
│   └── SettingsViewModel.cs          # Privacy toggles, auto-save commands, and slider bindings
│
├── MainWindow.xaml / .cs             # Primary WinUI 3 window, centralized wheel routing & hotkeys
├── SettingsPage.xaml / .cs           # Privacy Engine configuration view
│
├── ml/                               # Machine Learning Training & Evaluation Pipeline
│   ├── scripts/                      # Training, dataset generation, challenge sets & ONNX export
│   ├── dataset/                      # Context generators, entities & taxonomy definitions
│   └── data/                         # Challenge sets & reference evaluation output
│
└── Tests/ClipboardManager.UnitTests/ # Comprehensive unit test suite (234 tests)
```

---

## Getting Started

### 1. Prerequisites
Ensure you have the following installed:
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) (Version 17.8 or newer) with the **.NET Desktop Development** workload and **Windows App SDK C# Templates**.

### 2. Building from Source
Clone the repository and build the project:

```powershell
git clone https://github.com/RaymundGerardReyes/Snippeter.git
cd Snippeter\ClipboardManager
dotnet build -c Release
```

### 3. Running Unit Tests
Execute the full test suite (covering tokenizers, BIO span decoding, masking regexes, settings toggles, and SQLite migrations):

```powershell
dotnet test ..\Tests\ClipboardManager.UnitTests\ClipboardManager.UnitTests.csproj
```

### 4. Publishing the Standalone Production Executable
To create a fully self-contained, unpackaged x64 `.exe` with embedded runtimes:

```powershell
dotnet publish ClipboardManager.csproj -c Release -r win-x64 --self-contained true
```

The output binary will be located at:
`bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish\ClipboardManager.exe`

---

## Configuration Reference

Settings are configured via the in-app **Settings View** or persisted in `privacy_settings`:

| Setting | Default | Description |
| :--- | :---: | :--- |
| `EnablePrivacyProtection` | `true` | **Master Switch**. When `false`, all masking is bypassed and text is stored 100% raw. |
| `MaskPasswords` | `true` | Masks passwords, connection string credentials, and URL userinfo passwords. |
| `MaskSecretsAndTokens` | `true` | Masks API tokens, JWTs, private keys, AWS/GitHub/Slack credentials. |
| `MaskEmails` | `true` | Masks email addresses. |
| `MaskPhones` | `true` | Masks domestic and international phone numbers. |
| `MaskPrivateIp` | `true` | Masks RFC 1918 private IPv4 addresses (`10.x`, `192.168.x`, `172.16-31.x`). |
| `MaskPublicIp` | `true` | Masks public IPv4 addresses (excluding allowlisted IPs). |
| `MaskDomainNames` | `true` | Masks domain names and hostnames (excluding allowlisted domains). |
| `MaskPortNumbers` | `true` | Masks port numbers in host:port pairs. |
| `MaskDatabaseNames` | `true` | Masks database catalog and schema identifiers. |
| `MaskHashIds` | `true` | Masks SHA-256, SHA-1, and MD5 hashes found in context. |
| `EnableDoubleLayerMasking` | `true` | Enables secondary structural masking mode. |
| `EnableMlSecretDetection` | `false` | Enables on-device DirectML GPU transformer secret detection. |
| `MlConfidenceThreshold` | `0.75` | Minimum confidence threshold (0.0 to 1.0) for ML secret predictions. |

---

## Diagnostics & Troubleshooting

### Diagnostic Logs
Snippeter writes thread-safe diagnostics and crash reports to:
`%LOCALAPPDATA%\ClipboardManager\crash.log`

To view the live startup milestones and diagnostic logs:
```powershell
Get-Content "$env:LOCALAPPDATA\ClipboardManager\crash.log" -Tail 30
```

Sample successful startup output:
```text
[2026-09-10 23:51:55.920] Program.Main entered.
[2026-09-10 23:51:56.124] Application Starting...
[2026-09-10 23:51:56.202] OnLaunched entered.
[2026-09-10 23:51:56.202] 1. Initialize SQLite Database...
[2026-09-10 23:51:56.280] 2. Start Background Expiration Sweeper...
[2026-09-10 23:51:56.282] 3. Initialize Secure Pipeline Services...
[2026-09-10 23:51:56.283] ML models path: '...\Models\ml' (Exists: True)
[2026-09-10 23:51:56.413] Starting Clipboard Monitor...
[2026-09-10 23:51:56.437] Initializing hotkey service...
[2026-09-10 23:51:56.437] Creating MainWindow...
[2026-09-10 23:51:57.250] Showing AppWindow...
[2026-09-10 23:51:57.263] MainWindow activated successfully. Startup completed.
[2026-09-10 23:51:57.545] ML Model Loader finished. Status: Loaded, Loaded: True
```

### Inspecting Local Data
To verify tables and stored records using Python or the SQLite CLI:
```powershell
python -c "import sqlite3, os; conn = sqlite3.connect(os.path.expandvars(r'%LOCALAPPDATA%\ClipboardManager\clipboard_history.db')); print(conn.execute('SELECT count(*) FROM clipboard_items').fetchone())"
```

---

## Roadmap & Active Goals

- [x] **v1.0.0**: Initial WinUI 3 Clipboard Manager, SQLite FTS5 search, Win32 global hotkeys.
- [x] **v1.1.0**: DirectML ONNX secret detection integration, BERT tokenizer, BIO span decoding.
- [x] **v1.2.0**: Settings view integration, Master Privacy Switch, zero-masking guarantee, 0ms instant mouse wheel scrolling.
- [x] **v1.2.1**: Self-healing SQLite schema (`privacy_settings`), thread-safe logging, and multi-path ML discovery hierarchy.
- [ ] **v1.3.0 (Planned)**:
  - Multi-format clipboard support (images, screenshots, and rich formatting).
  - Database encryption at rest via SQLCipher (AES-256).
  - In-app configurable keyboard shortcut manager.
  - Optional local LAN peer-to-peer encrypted sync.

---

## Contributing & License

Contributions are welcome! Please ensure all pull requests pass `dotnet test` with 100% pass rate before submitting.

Distributed under the **MIT License**. See `LICENSE` for more information.
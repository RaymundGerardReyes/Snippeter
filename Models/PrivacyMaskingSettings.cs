using System;
using System.Collections.Generic;

namespace ClipboardManager.Models
{
    public class PrivacyMaskingSettings
    {
        public static PrivacyMaskingSettings Default { get; } = new PrivacyMaskingSettings();

        // Layer 1 — Core Privacy Controls (Enabled by default)
        public bool MaskPrivateIp { get; set; } = true;
        public bool MaskPublicIp { get; set; } = true;
        public bool MaskDomainNames { get; set; } = true;
        public bool MaskPortNumbers { get; set; } = true;
        public bool MaskDatabaseNames { get; set; } = true;
        public bool MaskHashIds { get; set; } = true;
        public bool MaskEmails { get; set; } = true;
        public bool MaskPhones { get; set; } = true;
        public bool MaskPasswords { get; set; } = true;

        // Layer 2 — User Custom Allowlist & Blocklist
        public List<string> AllowedDomains { get; set; } = new();   // e.g. "mycompany.com" — never mask if matched
        public List<string> AllowedPublicIps { get; set; } = new(); // e.g. "8.8.8.8" — never mask if matched
        public List<string> CustomBlockedPatterns { get; set; } = new(); // Custom regexes to ALWAYS mask

        // Double-Layer Masking Controller Toggle
        public bool EnableDoubleLayerMasking { get; set; } = true;
        public MaskingMode DoubleLayerDefaultMode { get; set; } = MaskingMode.Full;

        // Phase 3 — Hybrid ML Layer 2.5 Settings
        public bool EnableMlSecretDetection { get; set; } = false;
        public double MlConfidenceThreshold { get; set; } = 0.75;
        public string? MlModelVersion { get; set; }
    }
}

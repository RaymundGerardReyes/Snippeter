using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ClipboardManager.Services;
using ClipboardManager.Models;

namespace RedactionHarness
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: RedactionHarness <input-log-file> <output-jsonl-file>");
                Environment.Exit(1);
            }

            var inputPath = args[0];
            var outputPath = args[1];
            
            // Ensure input file exists
            if (!File.Exists(inputPath))
            {
                Console.Error.WriteLine($"Error: Input file not found at {inputPath}");
                Environment.Exit(1);
            }

            // Ensure output directory exists
            var outDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
            {
                Directory.CreateDirectory(outDir);
            }

            // Initialize Phase 2 Classifier without ML injected to ensure pure Regex fallback logic
            var classifier = new PrivacyClassifier(null); 
            var settings = PrivacyMaskingSettings.Default; // full Layer 1 defaults, no allowlist exceptions for this pass

            using var writer = new StreamWriter(outputPath, append: false);
            int lineNumber = 0;
            int redactedCount = 0;

            foreach (var rawLine in File.ReadLines(inputPath))
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(rawLine)) continue;

                // 1. Analyze the line
                var result = classifier.Analyze(rawLine, settings);
                
                // 2. Safely extract the masked text
                var safePreview = MaskingPolicy.GenerateSafePreview(rawLine, result);
                
                // 3. Serialize finding metadata
                var record = new
                {
                    line_number = lineNumber,
                    original_length = rawLine.Length,
                    redacted_text = safePreview.SafeText, // Extracting the actual masked output
                    finding_count = result.Findings?.Count ?? 0,
                    categories = result.Findings?.Select(f => f.Category.ToString()).Distinct().ToArray() ?? Array.Empty<string>()
                };
                
                writer.WriteLine(JsonSerializer.Serialize(record));
                if (result.Findings?.Count > 0) redactedCount++;
            }

            Console.WriteLine($"Processed {lineNumber} lines. {redactedCount} lines had at least one redaction.");
        }
    }
}

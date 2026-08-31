using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClipboardManager.Models;

namespace ClipboardManager.Services.Ml
{
    public class MlSecretDetector : IMlSecretDetector
    {
        private const int ChunkSizeTokens = 256;
        private const int ChunkOverlapTokens = 48;
        
        // This threshold was used in prior tests and defined in PrivacyMaskingSettings, but we need
        // a fallback or we use a low enough threshold here and let PrivacyClassifier filter the rest.
        // The brief says "keep only one (prefer the finding with higher average confidence)".
        // So we will just output all findings (or apply a base threshold like 0.1) and deduplicate.
        private const double BaseConfidenceThreshold = 0.50;

        private readonly IMlModelLoader _modelLoader;
        private readonly ITokenizerService _tokenizer;
        private readonly IOnnxInferenceRunner _inferenceRunner;

        public bool IsAvailable => _modelLoader.IsModelLoaded && _tokenizer.IsReady;

        public MlSecretDetector(
            IMlModelLoader modelLoader,
            ITokenizerService tokenizer,
            IOnnxInferenceRunner inferenceRunner)
        {
            _modelLoader = modelLoader;
            _tokenizer = tokenizer;
            _inferenceRunner = inferenceRunner;
        }

        public async Task<IReadOnlyList<PrivacyFinding>> DetectAsync(string input, TimeSpan timeBudget, CancellationToken cancellationToken)
        {
            if (!IsAvailable || string.IsNullOrWhiteSpace(input))
            {
                return Array.Empty<PrivacyFinding>();
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeBudget);

            try
            {
                return await Task.Run(() => RunChunkedInference(input, cts.Token), cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Timed out on our own budget, not caller-cancelled — fail safe
                return Array.Empty<PrivacyFinding>();
            }
            catch
            {
                // Never weaken the fail-safe
                return Array.Empty<PrivacyFinding>();
            }
        }

        private IReadOnlyList<PrivacyFinding> RunChunkedInference(string input, CancellationToken token)
        {
            var findings = new List<PrivacyFinding>();

            // Tokenize entire input to get true token boundaries
            var (allIds, allMasks, allOffsets) = _tokenizer.Tokenize(input, int.MaxValue);
            if (allIds.Length == 0) return findings;

            // Known label schema for the model
            var labels = _modelLoader.Manifest?.Labels?.ToArray() ?? new[] {
                "O", "B-SECRET", "I-SECRET", "B-PII", "I-PII",
                "B-HOSTINFO", "I-HOSTINFO", "B-NETWORK", "I-NETWORK"
            };

            int stride = ChunkSizeTokens - ChunkOverlapTokens;
            if (stride <= 0) stride = ChunkSizeTokens;

            for (int startIdx = 0; startIdx < allIds.Length; startIdx += stride)
            {
                token.ThrowIfCancellationRequested();

                int currentChunkSize = Math.Min(ChunkSizeTokens, allIds.Length - startIdx);
                
                int[] chunkIds = new int[ChunkSizeTokens];
                int[] chunkMasks = new int[ChunkSizeTokens];
                var chunkOffsets = new (int Start, int Length)[ChunkSizeTokens];

                // Copy data, rest remains 0 (padding)
                Array.Copy(allIds, startIdx, chunkIds, 0, currentChunkSize);
                Array.Copy(allMasks, startIdx, chunkMasks, 0, currentChunkSize);
                Array.Copy(allOffsets, startIdx, chunkOffsets, 0, currentChunkSize);

                var logits = _inferenceRunner.RunTokenClassification(chunkIds, chunkMasks, labels.Length);
                
                if (logits != null && logits.Length > 0)
                {
                    // Decode
                    var chunkFindings = BioSpanDecoder.Decode(logits, chunkOffsets, labels, BaseConfidenceThreshold);
                    
                    // Add findings (filtering out any that were decoded entirely in the padding region)
                    // The decoder should naturally ignore padding since labels there would typically be "O"
                    // But we ensure we only take valid findings
                    foreach (var f in chunkFindings)
                    {
                        if (f.Length > 0 && f.StartIndex >= chunkOffsets[0].Start)
                        {
                            findings.Add(f);
                        }
                    }
                }

                if (currentChunkSize < ChunkSizeTokens)
                {
                    break; // reached the end
                }
            }

            return DeduplicateFindings(findings);
        }

        private IReadOnlyList<PrivacyFinding> DeduplicateFindings(List<PrivacyFinding> findings)
        {
            if (findings.Count <= 1) return findings;

            var deduplicated = new List<PrivacyFinding>();
            // Sort by start index
            var sorted = findings.OrderBy(f => f.StartIndex).ThenByDescending(f => f.Length).ToList();

            foreach (var current in sorted)
            {
                bool isDuplicate = false;
                for (int j = 0; j < deduplicated.Count; j++)
                {
                    var existing = deduplicated[j];
                    
                    // Check for overlap
                    int overlapStart = Math.Max(current.StartIndex, existing.StartIndex);
                    int overlapEnd = Math.Min(current.StartIndex + current.Length, existing.StartIndex + existing.Length);
                    
                    if (overlapStart < overlapEnd) // Overlapping region
                    {
                        // In the overlap region, keep only one (prefer higher confidence, or first if tie)
                        if (current.Confidence > existing.Confidence)
                        {
                            deduplicated[j] = current; // Replace with better finding
                        }
                        isDuplicate = true;
                        break;
                    }
                }

                if (!isDuplicate)
                {
                    deduplicated.Add(current);
                }
            }

            return deduplicated;
        }
    }
}

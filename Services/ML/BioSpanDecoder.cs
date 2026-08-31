using System;
using System.Collections.Generic;
using ClipboardManager.Models;

namespace ClipboardManager.Services.Ml
{
    public static class BioSpanDecoder
    {
        public static IReadOnlyList<PrivacyFinding> Decode(
            float[][] logits,
            (int Start, int Length)[] tokenOffsets,
            string[] labelNames,
            double confidenceThreshold)
        {
            var findings = new List<PrivacyFinding>();

            if (logits == null || tokenOffsets == null || logits.Length == 0 || logits.Length != tokenOffsets.Length)
            {
                return findings;
            }

            int seqLen = logits.Length;
            
            string? currentEntityCategory = null;
            double currentEntityConfidenceSum = 0.0;
            int currentEntityTokenCount = 0;
            int currentEntityStartChar = -1;
            int currentEntityEndChar = -1;

            for (int i = 0; i < seqLen; i++)
            {
                // Softmax
                var tokenLogits = logits[i];
                double maxVal = tokenLogits[0];
                for (int j = 1; j < tokenLogits.Length; j++) maxVal = Math.Max(maxVal, tokenLogits[j]);
                
                double sumExp = 0.0;
                for (int j = 0; j < tokenLogits.Length; j++) sumExp += Math.Exp(tokenLogits[j] - maxVal);
                
                int bestLabelIdx = 0;
                double bestConfidence = Math.Exp(tokenLogits[0] - maxVal) / sumExp;

                for (int j = 1; j < tokenLogits.Length; j++)
                {
                    double prob = Math.Exp(tokenLogits[j] - maxVal) / sumExp;
                    if (prob > bestConfidence)
                    {
                        bestConfidence = prob;
                        bestLabelIdx = j;
                    }
                }

                string label = labelNames[bestLabelIdx];
                var offset = tokenOffsets[i];

                bool isB = label.StartsWith("B-");
                bool isI = label.StartsWith("I-");
                string category = (isB || isI) ? label.Substring(2) : string.Empty;

                bool shouldFinalizeCurrent = false;

                if (label == "O")
                {
                    shouldFinalizeCurrent = true;
                }
                else if (isB)
                {
                    shouldFinalizeCurrent = true;
                }
                else if (isI)
                {
                    if (currentEntityCategory != category)
                    {
                        shouldFinalizeCurrent = true;
                    }
                }

                if (shouldFinalizeCurrent && currentEntityCategory != null)
                {
                    double avgConfidence = currentEntityConfidenceSum / currentEntityTokenCount;
                    if (avgConfidence >= confidenceThreshold)
                    {
                        findings.Add(CreateFinding(currentEntityCategory, currentEntityStartChar, currentEntityEndChar - currentEntityStartChar));
                    }
                    currentEntityCategory = null;
                }

                if (isB || (isI && currentEntityCategory == null))
                {
                    currentEntityCategory = category;
                    currentEntityConfidenceSum = bestConfidence;
                    currentEntityTokenCount = 1;
                    currentEntityStartChar = offset.Start;
                    currentEntityEndChar = offset.Start + offset.Length;
                }
                else if (isI && currentEntityCategory == category)
                {
                    currentEntityConfidenceSum += bestConfidence;
                    currentEntityTokenCount++;
                    currentEntityEndChar = offset.Start + offset.Length; // Extend to include this token
                }
            }

            // Finalize any open entity at the end of the sequence
            if (currentEntityCategory != null)
            {
                double avgConfidence = currentEntityConfidenceSum / currentEntityTokenCount;
                if (avgConfidence >= confidenceThreshold)
                {
                    findings.Add(CreateFinding(currentEntityCategory, currentEntityStartChar, currentEntityEndChar - currentEntityStartChar));
                }
            }

            return findings;
        }

        private static PrivacyFinding CreateFinding(string mlCategory, int index, int length)
        {
            // Map ML schema string to the strongly-typed PrivacyCategory enum
            // The model produces categories like "SECRET", "PII", "HOSTINFO", "NETWORK"
            PrivacyCategory category = PrivacyCategory.Unknown;
            
            if (mlCategory == "SECRET") category = PrivacyCategory.Password; // Map general secret to Password for now
            else if (mlCategory == "PII") category = PrivacyCategory.Email;
            else if (mlCategory == "HOSTINFO") category = PrivacyCategory.Domain;
            else if (mlCategory == "NETWORK") category = PrivacyCategory.PublicIp;

            return new PrivacyFinding(category, index, length, FindingSource.MachineLearning);
        }
    }
}

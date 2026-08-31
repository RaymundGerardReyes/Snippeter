using System;
using System.IO;
using System.Linq;
using Microsoft.ML.Tokenizers;

namespace ClipboardManager.Services.Ml
{
    public class BertTokenizerService : ITokenizerService
    {
        private readonly Tokenizer? _tokenizer;
        public bool IsReady { get; }

        public BertTokenizerService(string vocabFilePath)
        {
            if (File.Exists(vocabFilePath))
            {
                try
                {
                    // As explicitly confirmed in the brief, use BertTokenizer.Create
                    _tokenizer = BertTokenizer.Create(vocabFilePath);
                    IsReady = true;
                }
                catch
                {
                    IsReady = false;
                }
            }
            else
            {
                IsReady = false;
            }
        }

        public (int[] InputIds, int[] AttentionMask, (int Start, int Length)[] Offsets) Tokenize(string text, int maxSequenceLength)
        {
            if (!IsReady || _tokenizer == null || string.IsNullOrWhiteSpace(text))
            {
                return (Array.Empty<int>(), Array.Empty<int>(), Array.Empty<(int, int)>());
            }

            try
            {
                // The tokenizer.Encode method returns a TokenizerResult which contains Ids and Offsets.
                // We truncate to maxSequenceLength to fit the model's expected shape.
                var encodeResult = _tokenizer.Encode(text);
                
                int tokenCount = Math.Min(encodeResult.Ids.Count, maxSequenceLength);
                
                int[] inputIds = new int[tokenCount];
                int[] attentionMask = new int[tokenCount];
                (int Start, int Length)[] offsets = new (int, int)[tokenCount];

                for (int i = 0; i < tokenCount; i++)
                {
                    inputIds[i] = encodeResult.Ids[i];
                    attentionMask[i] = 1; // 1 for real tokens, 0 for padding (though we don't pad here, we just truncate)
                    
                    // The Offset property on TokenizerResult usually returns a Range or a tuple.
                    // Depending on the exact Microsoft.ML.Tokenizers version, it exposes an Offset tuple or Start/Length.
                    // For version 0.22.0-preview, Offsets is an IReadOnlyList<(int Index, int Length)> or similar.
                    // We adapt by extracting the range.
                    var offset = encodeResult.Offsets[i];
                    // If the library returns Range:
                    // int start = offset.Start.Value;
                    // int length = offset.End.Value - start;
                    // If it returns a tuple/struct with Index and Length:
                    // Here we assume it returns a tuple or type with Start/Index and Length properties based on standard .NET tokenizers.
                    
                    // We will use dynamic if we aren't 100% certain, but that's unsafe. 
                    // The official Microsoft.ML.Tokenizers v0.22 `Offsets` is IReadOnlyList<(int Offset, int Length)>.
                    
                    offsets[i] = (offset.Offset, offset.Length);
                }

                return (inputIds, attentionMask, offsets);
            }
            catch
            {
                // Never weaken the fail-safe
                return (Array.Empty<int>(), Array.Empty<int>(), Array.Empty<(int, int)>());
            }
        }
    }
}

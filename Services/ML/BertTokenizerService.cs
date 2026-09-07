using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ClipboardManager.Services.Ml
{
    public class BertTokenizerService : ITokenizerService
    {
        private readonly Dictionary<string, int> _vocab = new(StringComparer.Ordinal);
        private readonly int _clsId = 101;
        private readonly int _sepId = 102;
        private readonly int _unkId = 100;

        public bool IsReady { get; }

        public BertTokenizerService(string vocabFilePath)
        {
            if (File.Exists(vocabFilePath))
            {
                try
                {
                    string[] lines = File.ReadAllLines(vocabFilePath);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string token = lines[i].Trim();
                        if (!string.IsNullOrEmpty(token) && !_vocab.ContainsKey(token))
                        {
                            _vocab[token] = i;
                        }
                    }

                    if (_vocab.TryGetValue("[CLS]", out int cls)) _clsId = cls;
                    if (_vocab.TryGetValue("[SEP]", out int sep)) _sepId = sep;
                    if (_vocab.TryGetValue("[UNK]", out int unk)) _unkId = unk;

                    IsReady = _vocab.Count > 0;
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

        private static bool IsBertPunctuation(char c)
        {
            if (char.IsPunctuation(c) || char.IsSymbol(c)) return true;
            return false;
        }

        public (int[] InputIds, int[] AttentionMask, (int Start, int Length)[] Offsets) Tokenize(string text, int maxSequenceLength)
        {
            if (!IsReady || string.IsNullOrWhiteSpace(text))
            {
                return (Array.Empty<int>(), Array.Empty<int>(), Array.Empty<(int, int)>());
            }

            var tokenIds = new List<int> { _clsId };
            var offsets = new List<(int Start, int Length)> { (0, 0) };

            int textLen = text.Length;
            int i = 0;

            while (i < textLen && tokenIds.Count < maxSequenceLength - 1)
            {
                // Skip whitespace
                if (char.IsWhiteSpace(text[i]))
                {
                    i++;
                    continue;
                }

                // Check punctuation & symbols as single character tokens
                int wordStart = i;
                if (IsBertPunctuation(text[i]))
                {
                    string punct = text[i].ToString();
                    int id = _vocab.TryGetValue(punct, out int pId) ? pId : _unkId;
                    tokenIds.Add(id);
                    offsets.Add((wordStart, 1));
                    i++;
                    continue;
                }

                // Read word token until whitespace or punctuation
                while (i < textLen && !char.IsWhiteSpace(text[i]) && !IsBertPunctuation(text[i]))
                {
                    i++;
                }

                string word = text.Substring(wordStart, i - wordStart);
                WordPieceTokenize(word, wordStart, tokenIds, offsets, maxSequenceLength - 1);
            }

            if (tokenIds.Count < maxSequenceLength)
            {
                tokenIds.Add(_sepId);
                offsets.Add((0, 0));
            }

            int count = tokenIds.Count;
            int[] inputIds = tokenIds.ToArray();
            int[] attentionMask = Enumerable.Repeat(1, count).ToArray();
            (int Start, int Length)[] offsetArray = offsets.ToArray();

            return (inputIds, attentionMask, offsetArray);
        }

        private void WordPieceTokenize(string word, int wordStart, List<int> tokenIds, List<(int Start, int Length)> offsets, int maxTokens)
        {
            if (tokenIds.Count >= maxTokens) return;

            string lowerWord = word.ToLowerInvariant();
            if (_vocab.TryGetValue(lowerWord, out int directId))
            {
                tokenIds.Add(directId);
                offsets.Add((wordStart, word.Length));
                return;
            }

            int start = 0;
            bool isBad = false;
            var subwordTokens = new List<int>();
            var subwordOffsets = new List<(int Start, int Length)>();

            while (start < lowerWord.Length)
            {
                int end = lowerWord.Length;
                int curId = -1;
                string? curSubword = null;

                while (start < end)
                {
                    string substr = lowerWord.Substring(start, end - start);
                    if (start > 0)
                    {
                        substr = "##" + substr;
                    }

                    if (_vocab.TryGetValue(substr, out int id))
                    {
                        curId = id;
                        curSubword = substr;
                        break;
                    }
                    end--;
                }

                if (curId == -1)
                {
                    isBad = true;
                    break;
                }

                subwordTokens.Add(curId);
                int subLen = end - start;
                subwordOffsets.Add((wordStart + start, subLen));
                start = end;
            }

            if (isBad || subwordTokens.Count == 0)
            {
                tokenIds.Add(_unkId);
                offsets.Add((wordStart, word.Length));
            }
            else
            {
                for (int idx = 0; idx < subwordTokens.Count && tokenIds.Count < maxTokens; idx++)
                {
                    tokenIds.Add(subwordTokens[idx]);
                    offsets.Add(subwordOffsets[idx]);
                }
            }
        }
    }
}

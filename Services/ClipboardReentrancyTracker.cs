using System;
using System.Collections.Generic;

namespace ClipboardManager.Services
{
    public class ClipboardReentrancyTracker : IReentrancyTracker
    {
        private readonly Dictionary<string, int> _expectedWrites = new(StringComparer.Ordinal);
        private readonly object _gate = new();

        public void RegisterProgrammaticWrite(string expectedText)
        {
            lock (_gate)
            {
                _expectedWrites.TryGetValue(expectedText, out var count);
                _expectedWrites[expectedText] = count + 1;
            }
        }

        public void CancelProgrammaticWrite(string expectedText)
        {
            lock (_gate)
            {
                if (_expectedWrites.TryGetValue(expectedText, out var count))
                {
                    if (count <= 1)
                        _expectedWrites.Remove(expectedText);
                    else
                        _expectedWrites[expectedText] = count - 1;
                }
            }
        }

        public bool ConsumeExpectedWrite(string rawText)
        {
            lock (_gate)
            {
                if (_expectedWrites.TryGetValue(rawText, out var count))
                {
                    if (count <= 1)
                        _expectedWrites.Remove(rawText);
                    else
                        _expectedWrites[rawText] = count - 1;
                    return true;
                }
                return false;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ClipboardManager.Models;

namespace ClipboardManager.Services.Ml
{
    public class OnnxSecretDetector : IMlSecretDetector
    {
        private readonly MlModelLoader _loader;

        public bool IsAvailable => _loader.IsModelLoaded;
        public bool IsModelLoaded => _loader.IsModelLoaded;
        public string? ModelVersion => _loader.ModelVersion;

        public OnnxSecretDetector(MlModelLoader loader)
        {
            _loader = loader;
        }

        public Task<IReadOnlyList<PrivacyFinding>> DetectAsync(string input, TimeSpan timeBudget, CancellationToken cancellationToken = default)
        {
            if (!IsAvailable || string.IsNullOrWhiteSpace(input))
            {
                return Task.FromResult<IReadOnlyList<PrivacyFinding>>(Array.Empty<PrivacyFinding>());
            }

            var findings = new List<PrivacyFinding>();
            return Task.FromResult<IReadOnlyList<PrivacyFinding>>(findings);
        }
    }
}

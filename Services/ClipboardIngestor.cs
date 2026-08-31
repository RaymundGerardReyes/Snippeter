using System;
using System.Threading.Tasks;
using ClipboardManager.Models;
using ClipboardManager.Data;

namespace ClipboardManager.Services
{
    public class ClipboardIngestor : IClipboardIngestor
    {
        private readonly IClipboardRepository _repository;
        private readonly IPrivacyClassifier _classifier;
        private readonly IMaskingService _maskingService;
        private readonly IClipboardWriter _clipboardWriter;
        private readonly IReentrancyTracker _reentrancyTracker;
        private readonly IPrivacyMaskingSettingsProvider? _settingsProvider;

        public ClipboardIngestor(
            IClipboardRepository repository, 
            IPrivacyClassifier classifier,
            IMaskingService maskingService,
            IClipboardWriter clipboardWriter,
            IReentrancyTracker reentrancyTracker,
            IPrivacyMaskingSettingsProvider? settingsProvider = null)
        {
            _repository = repository;
            _classifier = classifier;
            _maskingService = maskingService;
            _clipboardWriter = clipboardWriter;
            _reentrancyTracker = reentrancyTracker;
            _settingsProvider = settingsProvider;
        }

        public Task<IngestionOutcome> ProcessNewContentAsync(string rawText, string? windowsId)
        {
            return ProcessNewContentAsync(rawText, () => Task.FromResult(windowsId));
        }

        public async Task<IngestionOutcome> ProcessNewContentAsync(string rawText, Func<Task<string?>>? historyIdFetcher = null)
        {
            if (string.IsNullOrWhiteSpace(rawText)) 
                return new IngestionOutcome(IngestionResult.Ignored, null);

            var settings = _settingsProvider?.GetCurrent() ?? PrivacyMaskingSettings.Default;
            var classification = _classifier.Analyze(rawText, settings);
            var protectionState = ClipboardProtectionState.Normal;
            string safeTextToStore = rawText;
            DateTimeOffset? expiration = null;

            if (classification.IsSensitive)
            {
                var maskResult = _maskingService.Apply(rawText, classification);

                if (maskResult == null || !maskResult.Success || string.IsNullOrWhiteSpace(maskResult.SafeText))
                    return new IngestionOutcome(IngestionResult.MaskingFailed, null);

                string maskedText = maskResult.SafeText;

                // Controller Option: Double-Layer Verification Pass
                if (settings.EnableDoubleLayerMasking)
                {
                    var secondPassClassification = _classifier.Analyze(maskedText, settings);
                    if (secondPassClassification.IsSensitive && secondPassClassification.MaskingPlan.Count > 0)
                    {
                        var secondMaskResult = _maskingService.Apply(maskedText, secondPassClassification);
                        if (secondMaskResult != null && secondMaskResult.Success && !string.IsNullOrWhiteSpace(secondMaskResult.SafeText))
                        {
                            maskedText = secondMaskResult.SafeText;
                        }
                    }
                }

                _reentrancyTracker.RegisterProgrammaticWrite(maskedText);
                var outcome = _clipboardWriter.WriteMaskedText(maskedText);
                
                if (outcome.Result == ClipboardWriteResult.Success)
                {
                    protectionState = ClipboardProtectionState.Protected;
                    safeTextToStore = maskedText;
                    expiration = DateTimeOffset.UtcNow.AddMinutes(15);
                }
                else
                {
                    _reentrancyTracker.CancelProgrammaticWrite(maskedText);
                    protectionState = ClipboardProtectionState.ReplacementFailed;
                    safeTextToStore = maskedText; 
                }
            }

            // Lazy Evaluation: Evaluate historyIdFetcher ONLY if the classification is Normal
            string? safeWindowsId = null;
            if (protectionState == ClipboardProtectionState.Normal && historyIdFetcher != null)
            {
                safeWindowsId = await historyIdFetcher();
            }

            var item = new ClipboardItem
            {
                WindowsId = safeWindowsId,
                ContentType = "Text",
                ProtectionState = protectionState,
                SafeText = safeTextToStore,
                PrimaryCategory = classification.IsSensitive && classification.Findings.Count > 0 
                    ? classification.Findings[0].Category 
                    : PrivacyCategory.Normal,
                ExpiresAt = expiration
            };

            var record = new ClipboardRecord
            {
                Item = item,
                Projection = new SearchProjection 
                { 
                    SearchText = protectionState == ClipboardProtectionState.Normal ? rawText : string.Empty,
                    ContainsSensitiveMaterial = classification.IsSensitive 
                }
            };

            try
            {
                await _repository.AddAsync(record);
                return protectionState == ClipboardProtectionState.ReplacementFailed 
                    ? new IngestionOutcome(IngestionResult.ReplacementFailed, item) 
                    : new IngestionOutcome(IngestionResult.Success, item);
            }
            catch (Exception)
            {
                return new IngestionOutcome(IngestionResult.PersistenceFailed, null);
            }
        }
    }
}

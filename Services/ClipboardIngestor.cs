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

        public Task<IngestionOutcome> ProcessNewContentAsync(string rawText, string? windowsId, System.Threading.CancellationToken cancellationToken = default)
        {
            var payload = new ClipboardPayload { Text = rawText, ContentType = "Text" };
            return ProcessNewContentAsync(payload, () => Task.FromResult(windowsId), cancellationToken);
        }

        public Task<IngestionOutcome> ProcessNewContentAsync(string rawText, Func<Task<string?>>? historyIdFetcher = null, System.Threading.CancellationToken cancellationToken = default)
        {
            var payload = new ClipboardPayload { Text = rawText, ContentType = "Text" };
            return ProcessNewContentAsync(payload, historyIdFetcher, cancellationToken);
        }

        public async Task<IngestionOutcome> ProcessNewContentAsync(ClipboardPayload payload, Func<Task<string?>>? historyIdFetcher = null, System.Threading.CancellationToken cancellationToken = default)
        {
            if (payload.ContentType == "Unknown" && string.IsNullOrWhiteSpace(payload.Text))
                return new IngestionOutcome(IngestionResult.Ignored, null);

            if (payload.ContentType == "Text" && string.IsNullOrWhiteSpace(payload.Text))
                return new IngestionOutcome(IngestionResult.Ignored, null);

            var settings = _settingsProvider?.GetCurrent() ?? PrivacyMaskingSettings.Default;
            ClassificationResult classification = new ClassificationResult { IsSensitive = false };
            
            string safeTextToStore = payload.Text ?? string.Empty;
            var protectionState = ClipboardProtectionState.Normal;
            DateTimeOffset? expiration = null;

            if (settings.EnablePrivacyProtection && !string.IsNullOrWhiteSpace(payload.Text))
            {
                int lineCount = 1;
                for (int i = 0; i < payload.Text.Length; i++)
                {
                    if (payload.Text[i] == '\n') lineCount++;
                    if (lineCount >= 1000) break;
                }

                classification = lineCount >= 1000 
                    ? await _classifier.AnalyzeAsync(payload.Text, settings, cancellationToken).ConfigureAwait(false) 
                    : _classifier.Analyze(payload.Text, settings);

                if (classification.IsSensitive)
                {
                    var maskResult = _maskingService.Apply(payload.Text, classification);
                    if (maskResult == null || !maskResult.Success || string.IsNullOrWhiteSpace(maskResult.SafeText))
                        return new IngestionOutcome(IngestionResult.MaskingFailed, null);

                    string maskedText = maskResult.SafeText;

                    if (settings.EnableDoubleLayerMasking)
                    {
                        var secondPass = _classifier.Analyze(maskedText, settings);
                        if (secondPass != null && secondPass.IsSensitive && secondPass.MaskingPlan != null && secondPass.MaskingPlan.Count > 0)
                        {
                            var secondMask = _maskingService.Apply(maskedText, secondPass);
                            if (secondMask != null && secondMask.Success && !string.IsNullOrWhiteSpace(secondMask.SafeText))
                            {
                                maskedText = secondMask.SafeText;
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
                        payload.Rtf = null; 
                        payload.Html = null;
                    }
                    else
                    {
                        _reentrancyTracker.CancelProgrammaticWrite(maskedText);
                        protectionState = ClipboardProtectionState.ReplacementFailed;
                        safeTextToStore = maskedText; 
                    }
                }
            }

            string? safeWindowsId = null;
            if (protectionState == ClipboardProtectionState.Normal && historyIdFetcher != null)
            {
                safeWindowsId = await historyIdFetcher();
            }

            var item = new ClipboardItem
            {
                WindowsId = safeWindowsId,
                ContentType = payload.ContentType,
                ProtectionState = protectionState,
                SafeText = safeTextToStore,
                PrimaryCategory = classification.IsSensitive && classification.Findings.Count > 0 
                    ? classification.Findings[0].Category : PrivacyCategory.Normal,
                ExpiresAt = expiration
            };

            var record = new ClipboardRecord
            {
                Item = item,
                Projection = new SearchProjection 
                { 
                    SearchText = protectionState == ClipboardProtectionState.Normal ? safeTextToStore : string.Empty,
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

using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using ClipboardManager.Models;
using ClipboardManager.Data;

namespace ClipboardManager.Services
{
    public class ClipboardIngestor
    {
        private readonly IClipboardRepository _repository;
        private readonly IPrivacyClassifier _classifier;
        private readonly ContentExtractionService _extractor;

        public ClipboardIngestor(
            IClipboardRepository repository, 
            IPrivacyClassifier classifier,
            ContentExtractionService extractor)
        {
            _repository = repository;
            _classifier = classifier;
            _extractor = extractor;
        }

        public async Task ProcessNewItemAsync(ClipboardHistoryItem nativeItem)
        {
            // 1. Content Extraction
            string rawText = await _extractor.ExtractTextAsync(nativeItem);
            if (string.IsNullOrWhiteSpace(rawText)) return;

            // 2. Classify
            ClassificationResult classification = _classifier.Analyze(rawText);

            // 3. Mask
            MaskingResult maskingResult = MaskingPolicy.GenerateSafePreview(rawText, classification);

            // 4. Search Projection
            var searchProjection = new SearchProjection
            {
                ContainsSensitiveMaterial = classification.IsSensitive,
                SearchText = classification.IsSensitive ? string.Empty : rawText
            };

            // 5. Data Model
            var item = new ClipboardItem
            {
                WindowsId = nativeItem.Id,
                ContentType = "Text",
                IsSensitive = classification.IsSensitive,
                MaskedPreview = maskingResult.MaskedText,
                PrimaryCategory = classification.IsSensitive && classification.Findings.Count > 0 
                    ? classification.Findings[0].Category 
                    : PrivacyCategory.Normal,
                StorageState = classification.IsSensitive ? StorageState.Protected : StorageState.WindowsOnly,
                ExpiresAt = classification.IsSensitive ? DateTimeOffset.Now.AddMinutes(15) : null
            };

            var record = new ClipboardRecord
            {
                Item = item,
                Projection = searchProjection
            };

            // 6. Persist
            await _repository.AddAsync(record);
        }
    }
}

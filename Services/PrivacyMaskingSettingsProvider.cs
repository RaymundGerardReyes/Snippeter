using System.Threading.Tasks;
using ClipboardManager.Data;
using ClipboardManager.Models;

namespace ClipboardManager.Services
{
    public class PrivacyMaskingSettingsProvider : IPrivacyMaskingSettingsProvider
    {
        private readonly ISettingsRepository? _repository;
        private PrivacyMaskingSettings _currentCache = PrivacyMaskingSettings.Default;

        public PrivacyMaskingSettingsProvider(ISettingsRepository? repository = null)
        {
            _repository = repository;
            if (_repository != null)
            {
                try
                {
                    _currentCache = _repository.GetSettingsAsync().GetAwaiter().GetResult() ?? PrivacyMaskingSettings.Default;
                }
                catch
                {
                    _currentCache = PrivacyMaskingSettings.Default;
                }
            }
        }

        public PrivacyMaskingSettingsProvider(PrivacyMaskingSettings initialSettings)
        {
            _currentCache = initialSettings ?? PrivacyMaskingSettings.Default;
        }

        public PrivacyMaskingSettings GetCurrent() => _currentCache;

        public void Update(PrivacyMaskingSettings settings)
        {
            _currentCache = settings ?? PrivacyMaskingSettings.Default;
            if (_repository != null)
            {
                _ = _repository.SaveSettingsAsync(_currentCache);
            }
        }
    }
}

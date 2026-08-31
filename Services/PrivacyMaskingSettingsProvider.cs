using System.Threading.Tasks;
using ClipboardManager.Data;
using ClipboardManager.Models;

namespace ClipboardManager.Services
{
    public class PrivacyMaskingSettingsProvider : IPrivacyMaskingSettingsProvider
    {
        private readonly SettingsRepository _repository;
        private PrivacyMaskingSettings _currentCache;

        public PrivacyMaskingSettings Current => _currentCache ??= new PrivacyMaskingSettings();

        public PrivacyMaskingSettingsProvider(SettingsRepository repository)
        {
            _repository = repository;
        }

        public async Task ReloadAsync()
        {
            _currentCache = await _repository.GetAsync();
        }
    }
}

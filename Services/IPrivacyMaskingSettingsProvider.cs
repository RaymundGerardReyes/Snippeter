using ClipboardManager.Models;

namespace ClipboardManager.Services
{
    public interface IPrivacyMaskingSettingsProvider
    {
        PrivacyMaskingSettings GetCurrent();
        void Update(PrivacyMaskingSettings settings);
    }

    public class InMemoryPrivacyMaskingSettingsProvider : IPrivacyMaskingSettingsProvider
    {
        private PrivacyMaskingSettings _current = PrivacyMaskingSettings.Default;

        public PrivacyMaskingSettings GetCurrent() => _current;

        public void Update(PrivacyMaskingSettings settings)
        {
            _current = settings ?? PrivacyMaskingSettings.Default;
        }
    }
}

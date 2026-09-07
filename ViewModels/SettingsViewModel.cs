using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ClipboardManager.Helpers;
using ClipboardManager.Models;
using ClipboardManager.Services;

namespace ClipboardManager.ViewModels
{
    public class SettingsViewModel : ObservableObject
    {
        private readonly IPrivacyMaskingSettingsProvider _settingsProvider;
        private bool _isLoading;

        private bool _enablePrivacyProtection;
        private bool _maskPasswords;
        private bool _maskSecretsAndTokens;
        private bool _maskEmails;
        private bool _maskPhones;
        private bool _maskPrivateIp;
        private bool _maskPublicIp;
        private bool _maskDomainNames;
        private bool _maskPortNumbers;
        private bool _maskDatabaseNames;
        private bool _maskHashIds;
        private bool _enableDoubleLayerMasking;
        private bool _enableMlSecretDetection;
        private double _mlConfidenceThreshold;
        private string? _mlModelVersion;

        private string _newAllowedDomain = string.Empty;
        private string _newBlockedPattern = string.Empty;

        public bool EnablePrivacyProtection { get => _enablePrivacyProtection; set => SetSetting(ref _enablePrivacyProtection, value); }
        public bool MaskPasswords { get => _maskPasswords; set => SetSetting(ref _maskPasswords, value); }
        public bool MaskSecretsAndTokens { get => _maskSecretsAndTokens; set => SetSetting(ref _maskSecretsAndTokens, value); }
        public bool MaskEmails { get => _maskEmails; set => SetSetting(ref _maskEmails, value); }
        public bool MaskPhones { get => _maskPhones; set => SetSetting(ref _maskPhones, value); }
        public bool MaskPrivateIp { get => _maskPrivateIp; set => SetSetting(ref _maskPrivateIp, value); }
        public bool MaskPublicIp { get => _maskPublicIp; set => SetSetting(ref _maskPublicIp, value); }
        public bool MaskDomainNames { get => _maskDomainNames; set => SetSetting(ref _maskDomainNames, value); }
        public bool MaskPortNumbers { get => _maskPortNumbers; set => SetSetting(ref _maskPortNumbers, value); }
        public bool MaskDatabaseNames { get => _maskDatabaseNames; set => SetSetting(ref _maskDatabaseNames, value); }
        public bool MaskHashIds { get => _maskHashIds; set => SetSetting(ref _maskHashIds, value); }
        public bool EnableDoubleLayerMasking { get => _enableDoubleLayerMasking; set => SetSetting(ref _enableDoubleLayerMasking, value); }
        public bool EnableMlSecretDetection { get => _enableMlSecretDetection; set => SetSetting(ref _enableMlSecretDetection, value); }
        public double MlConfidenceThreshold { get => _mlConfidenceThreshold; set => SetSetting(ref _mlConfidenceThreshold, value); }
        public string? MlModelVersion { get => _mlModelVersion; set => SetSetting(ref _mlModelVersion, value); }

        public string NewAllowedDomain { get => _newAllowedDomain; set => SetProperty(ref _newAllowedDomain, value); }
        public string NewBlockedPattern { get => _newBlockedPattern; set => SetProperty(ref _newBlockedPattern, value); }

        public ObservableCollection<string> AllowedDomains { get; } = new();
        public ObservableCollection<string> CustomBlockedPatterns { get; } = new();

        public ICommand SaveSettingsCommand { get; }
        public ICommand AddAllowedDomainCommand { get; }
        public ICommand RemoveAllowedDomainCommand { get; }
        public ICommand AddBlockedPatternCommand { get; }
        public ICommand RemoveBlockedPatternCommand { get; }

        public SettingsViewModel(IPrivacyMaskingSettingsProvider settingsProvider)
        {
            _settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));

            SaveSettingsCommand = new RelayCommand<object>(_ => SaveSettings());
            AddAllowedDomainCommand = new RelayCommand<object>(_ => AddAllowedDomain());
            RemoveAllowedDomainCommand = new RelayCommand<string>(domain => RemoveAllowedDomain(domain));
            AddBlockedPatternCommand = new RelayCommand<object>(_ => AddBlockedPattern());
            RemoveBlockedPatternCommand = new RelayCommand<string>(pattern => RemoveBlockedPattern(pattern));

            LoadSettings();
        }

        private void SetSetting<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (SetProperty(ref storage, value, propertyName))
            {
                if (!_isLoading)
                {
                    SaveSettings();
                }
            }
        }

        public void LoadSettings()
        {
            _isLoading = true;
            try
            {
                var s = _settingsProvider.GetCurrent() ?? PrivacyMaskingSettings.Default;
                EnablePrivacyProtection = s.EnablePrivacyProtection;
                MaskPasswords = s.MaskPasswords;
                MaskSecretsAndTokens = s.MaskSecretsAndTokens;
                MaskEmails = s.MaskEmails;
                MaskPhones = s.MaskPhones;
                MaskPrivateIp = s.MaskPrivateIp;
                MaskPublicIp = s.MaskPublicIp;
                MaskDomainNames = s.MaskDomainNames;
                MaskPortNumbers = s.MaskPortNumbers;
                MaskDatabaseNames = s.MaskDatabaseNames;
                MaskHashIds = s.MaskHashIds;
                EnableDoubleLayerMasking = s.EnableDoubleLayerMasking;
                EnableMlSecretDetection = s.EnableMlSecretDetection;
                MlConfidenceThreshold = s.MlConfidenceThreshold;
                MlModelVersion = s.MlModelVersion ?? "1.0.0 (Regex CPU Core)";

                AllowedDomains.Clear();
                if (s.AllowedDomains != null)
                {
                    foreach (var domain in s.AllowedDomains) AllowedDomains.Add(domain);
                }

                CustomBlockedPatterns.Clear();
                if (s.CustomBlockedPatterns != null)
                {
                    foreach (var pattern in s.CustomBlockedPatterns) CustomBlockedPatterns.Add(pattern);
                }
            }
            finally
            {
                _isLoading = false;
            }
        }

        public void SaveSettings()
        {
            var current = _settingsProvider.GetCurrent() ?? PrivacyMaskingSettings.Default;
            var s = new PrivacyMaskingSettings
            {
                EnablePrivacyProtection = EnablePrivacyProtection,
                MaskPasswords = MaskPasswords,
                MaskSecretsAndTokens = MaskSecretsAndTokens,
                MaskEmails = MaskEmails,
                MaskPhones = MaskPhones,
                MaskPrivateIp = MaskPrivateIp,
                MaskPublicIp = MaskPublicIp,
                MaskDomainNames = MaskDomainNames,
                MaskPortNumbers = MaskPortNumbers,
                MaskDatabaseNames = MaskDatabaseNames,
                MaskHashIds = MaskHashIds,
                AllowedPublicIps = current.AllowedPublicIps?.ToList() ?? new(),
                DoubleLayerDefaultMode = current.DoubleLayerDefaultMode,
                EnableDoubleLayerMasking = EnableDoubleLayerMasking,
                EnableMlSecretDetection = EnableMlSecretDetection,
                MlConfidenceThreshold = MlConfidenceThreshold,
                MlModelVersion = MlModelVersion,
                AllowedDomains = AllowedDomains.ToList(),
                CustomBlockedPatterns = CustomBlockedPatterns.ToList()
            };

            _settingsProvider.Update(s);
        }

        private void AddAllowedDomain()
        {
            if (!string.IsNullOrWhiteSpace(NewAllowedDomain) && !AllowedDomains.Contains(NewAllowedDomain.Trim()))
            {
                AllowedDomains.Add(NewAllowedDomain.Trim());
                NewAllowedDomain = string.Empty;
                SaveSettings();
            }
        }

        private void RemoveAllowedDomain(string? domain)
        {
            if (domain != null && AllowedDomains.Remove(domain))
            {
                SaveSettings();
            }
        }

        private void AddBlockedPattern()
        {
            if (!string.IsNullOrWhiteSpace(NewBlockedPattern) && !CustomBlockedPatterns.Contains(NewBlockedPattern.Trim()))
            {
                CustomBlockedPatterns.Add(NewBlockedPattern.Trim());
                NewBlockedPattern = string.Empty;
                SaveSettings();
            }
        }

        private void RemoveBlockedPattern(string? pattern)
        {
            if (pattern != null && CustomBlockedPatterns.Remove(pattern))
            {
                SaveSettings();
            }
        }
    }
}

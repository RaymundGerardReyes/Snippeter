using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ClipboardManager.Helpers;
using ClipboardManager.Models;
using ClipboardManager.Services;

namespace ClipboardManager.ViewModels
{
    public class SettingsViewModel : ObservableObject
    {
        private readonly IPrivacyMaskingSettingsProvider _settingsProvider;

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

        public bool MaskPrivateIp { get => _maskPrivateIp; set => SetProperty(ref _maskPrivateIp, value); }
        public bool MaskPublicIp { get => _maskPublicIp; set => SetProperty(ref _maskPublicIp, value); }
        public bool MaskDomainNames { get => _maskDomainNames; set => SetProperty(ref _maskDomainNames, value); }
        public bool MaskPortNumbers { get => _maskPortNumbers; set => SetProperty(ref _maskPortNumbers, value); }
        public bool MaskDatabaseNames { get => _maskDatabaseNames; set => SetProperty(ref _maskDatabaseNames, value); }
        public bool MaskHashIds { get => _maskHashIds; set => SetProperty(ref _maskHashIds, value); }
        public bool EnableDoubleLayerMasking { get => _enableDoubleLayerMasking; set => SetProperty(ref _enableDoubleLayerMasking, value); }
        public bool EnableMlSecretDetection { get => _enableMlSecretDetection; set => SetProperty(ref _enableMlSecretDetection, value); }
        public double MlConfidenceThreshold { get => _mlConfidenceThreshold; set => SetProperty(ref _mlConfidenceThreshold, value); }
        public string? MlModelVersion { get => _mlModelVersion; set => SetProperty(ref _mlModelVersion, value); }

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

        public void LoadSettings()
        {
            var s = _settingsProvider.GetCurrent() ?? PrivacyMaskingSettings.Default;
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

        public void SaveSettings()
        {
            var s = new PrivacyMaskingSettings
            {
                MaskPrivateIp = MaskPrivateIp,
                MaskPublicIp = MaskPublicIp,
                MaskDomainNames = MaskDomainNames,
                MaskPortNumbers = MaskPortNumbers,
                MaskDatabaseNames = MaskDatabaseNames,
                MaskHashIds = MaskHashIds,
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

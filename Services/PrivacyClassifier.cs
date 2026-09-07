using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ClipboardManager.Models;
using ClipboardManager.Services.Ml;

namespace ClipboardManager.Services
{
    public class PrivacyClassifier : IPrivacyClassifier
    {
        private static class ClassifyPolicy
        {
            public const float ConfidenceThreshold = 0.70f;
            public const int MaxStructuralProximityChars = 150; 
        }

        private readonly IMlSecretDetector? _mlSecretDetector;

        public PrivacyClassifier(IMlSecretDetector? mlSecretDetector = null)
        {
            _mlSecretDetector = mlSecretDetector;
        }

        // Static Compiled Regexes for 15,000+ line ultra-fast execution (<100ms)
        private static readonly Regex RxDatabaseUri = new(@"(?i)\b(?:postgres|postgresql|mysql|mssql|mongodb|sqlite|redis)://[^\s""']+", RegexOptions.Compiled);
        private static readonly Regex RxCredentialBearingUrl = new(@"(?i)(?:https?|ftp|sftp)://[^\s""']+", RegexOptions.Compiled);
        private static readonly Regex RxConnectionString = new(@"(?i)\b(?:Server|Data Source|Host|User ID|Database)\s*=[^;]+(?:;[^;]+=[^;]+)+\b", RegexOptions.Compiled);
        private static readonly Regex RxConnStrPwd = new(@"(?i)(?:Password|Pwd|Secret)\s*=\s*([^;\s""]+)", RegexOptions.Compiled);
        private static readonly Regex RxConnStrUser = new(@"(?i)(?:User ID|Uid|Username)\s*=\s*([^;\s""]+)", RegexOptions.Compiled);
        private static readonly Regex RxConnStrHost = new(@"(?i)(?:Server|Data Source|Host)\s*=\s*([^;\s""]+)", RegexOptions.Compiled);
        private static readonly Regex RxConnStrPort = new(@"(?i)\b(?:Port)\s*=\s*([0-9]+)", RegexOptions.Compiled);
        private static readonly Regex RxConnStrDb = new(@"(?i)(?:Database|Initial Catalog)\s*=\s*([^;\s""]+)", RegexOptions.Compiled);
        private static readonly Regex RxCloudCredential = new(@"(?im)\b(?:AWS_SECRET_ACCESS_KEY|AZURE_CLIENT_SECRET|GOOGLE_APPLICATION_CREDENTIALS)\s*=\s*([^""'\s]+)\b", RegexOptions.Compiled);
        private static readonly Regex RxAuthHeader = new(@"(?im)(?:^(?:Authorization|X-API-Key)\s*:\s*|\bBearer\s+)(?:(?:Bearer|Basic|Token)\s+)?([^\s]+)", RegexOptions.Compiled);
        private static readonly Regex RxJsonYamlSecret = new(@"(?im)(?:[""'](?:password|pwd|secret|token|api[_-]?key)[""']\s*:\s*[""']?([^""',\r\n{}]+)[""']?)", RegexOptions.Compiled);
        private static readonly Regex RxIpv4 = new(@"\b(?:\d{1,3}\.){3}\d{1,3}\b", RegexOptions.Compiled);
        private static readonly Regex RxIpv6 = new(@"(?<![a-zA-Z0-9])(?:[A-Fa-f0-9]{0,4}:){2,7}[A-Fa-f0-9]{0,4}(?![a-zA-Z0-9])", RegexOptions.Compiled);
        private static readonly Regex RxApiKey = new(@"\b(?:AKIA[0-9A-Z]{16}|sk-(?:live|test)-[a-zA-Z0-9]{24,}|ghp_[a-zA-Z0-9]{36}|ya29\.[a-zA-Z0-9_-]+|AIza[a-zA-Z0-9_-]{35})\b", RegexOptions.Compiled);
        private static readonly Regex RxJwt = new(@"ey[a-zA-Z0-9_-]+\.[a-zA-Z0-9_-]+\.[a-zA-Z0-9_-]+", RegexOptions.Compiled);
        private static readonly Regex RxPrivateKey = new(@"-----BEGIN (?:[A-Z]+ )?PRIVATE KEY-----.*?-----END (?:[A-Z]+ )?PRIVATE KEY-----", RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex RxEnvVar = new(@"(?im)^(?:export\s+)?[A-Z0-9_]*(?:PASSWORD|SECRET|TOKEN|KEY|DATABASE_URL|DB_URL|DATABASE_URI|DB_URI|REDIS_URL|MONGODB_URI|CONNECTION_STRING)[A-Z0-9_]*\s*=\s*""?([^""\s]+)""?", RegexOptions.Compiled);
        private static readonly Regex RxHashContext = new(@"(?i)\b(?:md5|sha1|sha256|sha512|checksum|hash)\s*[:=]\s*([a-fA-F0-9]{32,128})\b", RegexOptions.Compiled);
        private static readonly Regex RxStandaloneHashId = new(@"\b(?:[0-9a-fA-F]{32}|[0-9a-fA-F]{40}|[0-9a-fA-F]{64}|[0-9a-fA-F]{128})\b", RegexOptions.Compiled);
        private static readonly Regex RxHostname = new(@"\b(?:[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?\.)+[a-zA-Z]{2,}\b", RegexOptions.Compiled);
        private static readonly Regex RxEmail = new(@"\b[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}\b", RegexOptions.Compiled);
        private static readonly Regex RxPhone = new(@"(?<!-)\b\+?\d{1,3}?[-.\s]?\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}\b", RegexOptions.Compiled);
        private static readonly Regex RxCredentialKeyValue = new(@"(?i)\b(password|pwd|secret|token|key)\b\s*[:=]\s*([^\s""';]+)", RegexOptions.Compiled);

        static PrivacyClassifier()
        {
            // Pre-warm compiled regexes to eliminate JIT compilation latency on first invocation
            _ = RxDatabaseUri.IsMatch("");
            _ = RxCredentialBearingUrl.IsMatch("");
            _ = RxJwt.IsMatch("");
            _ = RxPrivateKey.IsMatch("");
            _ = RxEnvVar.IsMatch("");
            _ = RxHashContext.IsMatch("");
            _ = RxStandaloneHashId.IsMatch("");
            _ = RxHostname.IsMatch("");
            _ = RxEmail.IsMatch("");
            _ = RxPhone.IsMatch("");
            _ = RxCredentialKeyValue.IsMatch("");
            _ = RxConnStrPort.IsMatch("");
        }

        public ClassificationResult Classify(string rawText) => Analyze(rawText, null);

        public ClassificationResult Analyze(string rawText) => Analyze(rawText, null);

        public ClassificationResult Analyze(string rawText, PrivacyMaskingSettings? settings = null)
        {
            settings ??= PrivacyMaskingSettings.Default;

            if (string.IsNullOrEmpty(rawText) || !settings.EnablePrivacyProtection)
                return new ClassificationResult { IsSensitive = false };

            // When all individual privacy toggles are untoggled, return non-sensitive with zero masking
            if (!settings.MaskPasswords &&
                !settings.MaskSecretsAndTokens &&
                !settings.MaskEmails &&
                !settings.MaskPhones &&
                !settings.MaskPrivateIp &&
                !settings.MaskPublicIp &&
                !settings.MaskDomainNames &&
                !settings.MaskPortNumbers &&
                !settings.MaskDatabaseNames &&
                !settings.MaskHashIds &&
                (settings.CustomBlockedPatterns == null || settings.CustomBlockedPatterns.Count == 0))
            {
                return new ClassificationResult { IsSensitive = false };
            }

            var findingsBag = new ConcurrentBag<PrivacyFinding>();

            // Run detectors in parallel for 15,000+ line scale sub-100ms execution
            Action[] detectors = new Action[]
            {
                () => DetectDatabaseUris(rawText, findingsBag, settings),
                () => DetectCredentialBearingUrls(rawText, findingsBag, settings),
                () => DetectConnectionStrings(rawText, findingsBag, settings),
                () => DetectCloudCredentials(rawText, findingsBag, settings),
                () => DetectAuthHeaders(rawText, findingsBag, settings),
                () => DetectApiKeys(rawText, findingsBag, settings),
                () => DetectJwts(rawText, findingsBag, settings),
                () => DetectPrivateKeys(rawText, findingsBag, settings),
                () => DetectJsonYamlSecrets(rawText, findingsBag, settings),
                () => DetectEnvironmentVariables(rawText, findingsBag, settings),
                () => DetectHashInContext(rawText, findingsBag, settings),
                () => DetectStandaloneHashIds(rawText, findingsBag, settings),
                () => DetectNetworkIdentifiers(rawText, findingsBag, settings),
                () => DetectEmails(rawText, findingsBag, settings),
                () => DetectPhones(rawText, findingsBag, settings),
                () => DetectHostnamesAndDomains(rawText, findingsBag, settings),
                () => DetectCredentialKeyValues(rawText, findingsBag, settings),
                () => DetectCustomBlockedPatterns(rawText, findingsBag, settings)
            };

            if (rawText.Length > 2000)
            {
                Parallel.ForEach(detectors, detector => detector());
            }
            else
            {
                foreach (var detector in detectors) detector();
            }

            var findingsList = findingsBag.ToList();

            // Apply User Custom Allowlist Filter (e.g. AllowedDomains or AllowedPublicIps)
            var filteredFindings = ApplyAllowlistFilter(findingsList, rawText, settings);

            var normalizedFindings = filteredFindings
                .OrderBy(f => f.StartIndex)
                .ThenByDescending(f => f.Length)
                .ThenByDescending(f => f.Severity)
                .ThenByDescending(f => f.Confidence)
                .ToList();

            return EvaluatePolicyAndBuildPlan(rawText, normalizedFindings, settings);
        }

        public async Task<ClassificationResult> AnalyzeAsync(string input, PrivacyMaskingSettings? settings = null, System.Threading.CancellationToken cancellationToken = default)
        {
            settings ??= PrivacyMaskingSettings.Default;
            
            // Reuse existing sync pipeline exactly as-is for Layer 1
            var layer1Result = Analyze(input, settings); 

            if (!settings.EnablePrivacyProtection || !settings.MaskSecretsAndTokens || !settings.EnableMlSecretDetection || _mlSecretDetector == null || !_mlSecretDetector.IsAvailable)
            {
                return layer1Result;
            }

            // Generate safe text using existing MaskingPolicy GenerateSafePreview
            var previewResult = MaskingPolicy.GenerateSafePreview(input, layer1Result);
            string maskedSoFar = previewResult.SafeText ?? input;

            var mlBudget = TimeSpan.FromMilliseconds(1500);
            
            // Run ML Secret Detector
            var mlFindings = await _mlSecretDetector.DetectAsync(maskedSoFar, mlBudget, cancellationToken).ConfigureAwait(false);

            if (mlFindings == null || mlFindings.Count == 0)
            {
                return layer1Result;
            }

            // Accept findings above threshold
            var accepted = mlFindings.Where(f => f.Confidence >= settings.MlConfidenceThreshold).ToList();
            if (accepted.Count == 0)
            {
                return layer1Result;
            }

            // Combine and rebuild the masking plan
            var combinedFindings = layer1Result.Findings.Concat(accepted).ToList();
            
            return EvaluatePolicyAndBuildPlan(input, combinedFindings, settings);
        }

        private List<PrivacyFinding> ApplyAllowlistFilter(List<PrivacyFinding> findings, string rawText, PrivacyMaskingSettings settings)
        {
            if ((settings.AllowedDomains == null || settings.AllowedDomains.Count == 0) &&
                (settings.AllowedPublicIps == null || settings.AllowedPublicIps.Count == 0))
            {
                return findings;
            }

            var result = new List<PrivacyFinding>();
            foreach (var finding in findings)
            {
                string spanText = rawText.Substring(finding.StartIndex, finding.Length);

                if (finding.Category == PrivacyCategory.Domain || finding.Category == PrivacyCategory.Hostname)
                {
                    if (settings.AllowedDomains != null && settings.AllowedDomains.Any(ad => spanText.Equals(ad, StringComparison.OrdinalIgnoreCase) || spanText.EndsWith("." + ad, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue; // Preserve allowed domain
                    }
                }

                if (finding.Category == PrivacyCategory.PublicIp || finding.Category == PrivacyCategory.PrivateIp)
                {
                    if (settings.AllowedPublicIps != null && settings.AllowedPublicIps.Any(ip => spanText.Equals(ip, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue; // Preserve allowed IP
                    }
                }

                result.Add(finding);
            }
            return result;
        }

        private void DetectDatabaseUris(string text, ConcurrentBag<PrivacyFinding> findings, PrivacyMaskingSettings settings)
        {
            foreach (Match match in RxDatabaseUri.Matches(text))
            {
                if (Uri.TryCreate(match.Value, UriKind.Absolute, out var uri))
                {
                    findings.Add(new PrivacyFinding { Category = PrivacyCategory.DatabaseUrl, StartIndex = match.Index, Length = match.Length, Confidence = 0.99f, Severity = Severity.Low });
                    ExtractUriCredentialsSafely(match, findings, PrivacyCategory.DatabaseCredential, settings);
                    ExtractUriStructuralComponents(match, uri, findings, settings);
                }
            }
        }

        private void DetectCredentialBearingUrls(string text, ConcurrentBag<PrivacyFinding> findings, PrivacyMaskingSettings settings)
        {
            foreach (Match match in RxCredentialBearingUrl.Matches(text))
            {
                if (Uri.TryCreate(match.Value, UriKind.Absolute, out var uri))
                {
                    findings.Add(new PrivacyFinding { Category = PrivacyCategory.SensitiveUrl, StartIndex = match.Index, Length = match.Length, Confidence = 0.95f, Severity = Severity.Low });
                    ExtractUriCredentialsSafely(match, findings, PrivacyCategory.PasswordLike, settings);
                    ExtractUriStructuralComponents(match, uri, findings, settings);
                }
            }
        }

        private void ExtractUriCredentialsSafely(Match match, ConcurrentBag<PrivacyFinding> findings, PrivacyCategory passwordCategory, PrivacyMaskingSettings settings)
        {
            if (!settings.MaskPasswords) return;

            int userInfoStart = match.Value.IndexOf("://", StringComparison.Ordinal) + 3;
            int userInfoEnd = match.Value.IndexOf('@');
            
            if (userInfoStart >= 3 && userInfoEnd > userInfoStart)
            {
                string rawUserInfo = match.Value.Substring(userInfoStart, userInfoEnd - userInfoStart);
                int colonIdx = rawUserInfo.IndexOf(':');
                
                if (colonIdx >= 0)
                {
                    findings.Add(new PrivacyFinding { Category = PrivacyCategory.Username, StartIndex = match.Index + userInfoStart, Length = colonIdx, Confidence = 0.90f, Severity = Severity.Medium });
                    findings.Add(new PrivacyFinding { Category = passwordCategory, StartIndex = match.Index + userInfoStart + colonIdx + 1, Length = rawUserInfo.Length - colonIdx - 1, Confidence = 0.99f, Severity = Severity.Critical });
                }
                else
                {
                    findings.Add(new PrivacyFinding { Category = PrivacyCategory.Username, StartIndex = match.Index + userInfoStart, Length = rawUserInfo.Length, Confidence = 0.90f, Severity = Severity.Medium });
                }
            }
        }

        private void ExtractUriStructuralComponents(Match match, Uri uri, ConcurrentBag<PrivacyFinding> findings, PrivacyMaskingSettings settings)
        {
            // Mask Hostname/Domain or IP in URI based on respective settings
            if (!string.IsNullOrEmpty(uri.Host))
            {
                int hostIdx = match.Value.IndexOf(uri.Host, StringComparison.OrdinalIgnoreCase);
                if (hostIdx >= 0)
                {
                    if (IPAddress.TryParse(uri.Host, out var ip))
                    {
                        if (ip.AddressFamily == AddressFamily.InterNetwork)
                        {
                            byte[] bytes = ip.GetAddressBytes();
                            bool isPrivate = (bytes[0] == 10) || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) || (bytes[0] == 192 && bytes[1] == 168);
                            if (isPrivate && settings.MaskPrivateIp)
                            {
                                findings.Add(new PrivacyFinding
                                {
                                    Category = PrivacyCategory.PrivateIp,
                                    StartIndex = match.Index + hostIdx,
                                    Length = uri.Host.Length,
                                    Confidence = 0.95f,
                                    Severity = Severity.Low
                                });
                            }
                            else if (!isPrivate && settings.MaskPublicIp)
                            {
                                findings.Add(new PrivacyFinding
                                {
                                    Category = PrivacyCategory.PublicIp,
                                    StartIndex = match.Index + hostIdx,
                                    Length = uri.Host.Length,
                                    Confidence = 0.95f,
                                    Severity = Severity.Low
                                });
                            }
                        }
                        else if (ip.AddressFamily == AddressFamily.InterNetworkV6 && settings.MaskPublicIp)
                        {
                            findings.Add(new PrivacyFinding
                            {
                                Category = PrivacyCategory.Ipv6,
                                StartIndex = match.Index + hostIdx,
                                Length = uri.Host.Length,
                                Confidence = 0.95f,
                                Severity = Severity.Low
                            });
                        }
                    }
                    else if (settings.MaskDomainNames)
                    {
                        findings.Add(new PrivacyFinding
                        {
                            Category = PrivacyCategory.Hostname,
                            StartIndex = match.Index + hostIdx,
                            Length = uri.Host.Length,
                            Confidence = 0.95f,
                            Severity = Severity.Medium
                        });
                    }
                }
            }

            // Mask Port number in URI if explicitly present and enabled
            if (settings.MaskPortNumbers && uri.Port > 0)
            {
                string portStr = ":" + uri.Port;
                int portIdx = match.Value.IndexOf(portStr, StringComparison.Ordinal);
                if (portIdx >= 0)
                {
                    findings.Add(new PrivacyFinding
                    {
                        Category = PrivacyCategory.Port,
                        StartIndex = match.Index + portIdx + 1,
                        Length = portStr.Length - 1,
                        Confidence = 0.99f,
                        Severity = Severity.Medium
                    });
                }
            }

            // Mask Database name (path segment) if enabled
            if (settings.MaskDatabaseNames && !string.IsNullOrEmpty(uri.AbsolutePath) && uri.AbsolutePath.Length > 1)
            {
                string dbName = uri.AbsolutePath.TrimStart('/');
                int slashIdx = dbName.IndexOf('/');
                if (slashIdx > 0) dbName = dbName.Substring(0, slashIdx);

                if (!string.IsNullOrEmpty(dbName))
                {
                    int dbIdx = match.Value.IndexOf("/" + dbName, StringComparison.Ordinal);
                    if (dbIdx >= 0)
                    {
                        findings.Add(new PrivacyFinding
                        {
                            Category = PrivacyCategory.DatabaseName,
                            StartIndex = match.Index + dbIdx + 1,
                            Length = dbName.Length,
                            Confidence = 0.95f,
                            Severity = Severity.Medium
                        });
                    }
                }
            }
        }

        private void DetectConnectionStrings(string text, ConcurrentBag<PrivacyFinding> findings, PrivacyMaskingSettings settings)
        {
            foreach (Match match in RxConnectionString.Matches(text))
            {
                findings.Add(new PrivacyFinding { Category = PrivacyCategory.ConnectionString, StartIndex = match.Index, Length = match.Length, Confidence = 0.95f, Severity = Severity.Low });

                if (settings.MaskPasswords)
                {
                    foreach (Match pwdMatch in RxConnStrPwd.Matches(match.Value))
                        findings.Add(new PrivacyFinding { Category = PrivacyCategory.DatabaseCredential, StartIndex = match.Index + pwdMatch.Groups[1].Index, Length = pwdMatch.Groups[1].Length, Confidence = 0.99f, Severity = Severity.Critical });

                    foreach (Match userMatch in RxConnStrUser.Matches(match.Value))
                        findings.Add(new PrivacyFinding { Category = PrivacyCategory.Username, StartIndex = match.Index + userMatch.Groups[1].Index, Length = userMatch.Groups[1].Length, Confidence = 0.90f, Severity = Severity.Medium });
                }

                foreach (Match hostMatch in RxConnStrHost.Matches(match.Value))
                {
                    string hostVal = hostMatch.Groups[1].Value.Trim();
                    int startIdx = match.Index + hostMatch.Groups[1].Index;
                    int length = hostMatch.Groups[1].Length;

                    string hostOnly = hostVal;
                    int portSepIdx = hostVal.IndexOfAny(new[] { ':', ',' });
                    int hostLen = length;

                    if (portSepIdx > 0)
                    {
                        hostOnly = hostVal.Substring(0, portSepIdx);
                        hostLen = portSepIdx;

                        if (settings.MaskPortNumbers)
                        {
                            int portStart = startIdx + portSepIdx + 1;
                            int portLen = length - portSepIdx - 1;
                            if (portLen > 0)
                            {
                                findings.Add(new PrivacyFinding { Category = PrivacyCategory.Port, StartIndex = portStart, Length = portLen, Confidence = 0.95f, Severity = Severity.Medium });
                            }
                        }
                    }

                    if (IPAddress.TryParse(hostOnly, out var ip))
                    {
                        if (ip.AddressFamily == AddressFamily.InterNetwork)
                        {
                            byte[] bytes = ip.GetAddressBytes();
                            bool isPrivate = (bytes[0] == 10) || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) || (bytes[0] == 192 && bytes[1] == 168);
                            if (isPrivate && settings.MaskPrivateIp)
                            {
                                findings.Add(new PrivacyFinding { Category = PrivacyCategory.PrivateIp, StartIndex = startIdx, Length = hostLen, Confidence = 0.90f, Severity = Severity.Low });
                            }
                            else if (!isPrivate && settings.MaskPublicIp)
                            {
                                findings.Add(new PrivacyFinding { Category = PrivacyCategory.PublicIp, StartIndex = startIdx, Length = hostLen, Confidence = 0.90f, Severity = Severity.Low });
                            }
                        }
                        else if (ip.AddressFamily == AddressFamily.InterNetworkV6 && settings.MaskPublicIp)
                        {
                            findings.Add(new PrivacyFinding { Category = PrivacyCategory.Ipv6, StartIndex = startIdx, Length = hostLen, Confidence = 0.90f, Severity = Severity.Low });
                        }
                    }
                    else if (settings.MaskDomainNames)
                    {
                        findings.Add(new PrivacyFinding { Category = PrivacyCategory.Hostname, StartIndex = startIdx, Length = hostLen, Confidence = 0.90f, Severity = Severity.Medium });
                    }
                }

                if (settings.MaskPortNumbers)
                {
                    foreach (Match portMatch in RxConnStrPort.Matches(match.Value))
                    {
                        findings.Add(new PrivacyFinding { Category = PrivacyCategory.Port, StartIndex = match.Index + portMatch.Groups[1].Index, Length = portMatch.Groups[1].Length, Confidence = 0.95f, Severity = Severity.Medium });
                    }
                }

                if (settings.MaskDatabaseNames)
                {
                    foreach (Match dbMatch in RxConnStrDb.Matches(match.Value))
                        findings.Add(new PrivacyFinding { Category = PrivacyCategory.DatabaseName, StartIndex = match.Index + dbMatch.Groups[1].Index, Length = dbMatch.Groups[1].Length, Confidence = 0.90f, Severity = Severity.Medium });
                }
            }
        }

        private void DetectCloudCredentials(string text, ConcurrentBag<PrivacyFinding> findings, PrivacyMaskingSettings settings)
        {
            if (!settings.MaskSecretsAndTokens) return;
            foreach (Match match in RxCloudCredential.Matches(text))
                findings.Add(new PrivacyFinding { Category = PrivacyCategory.CloudCredential, StartIndex = match.Index, Length = match.Length, Confidence = 0.99f, Severity = Severity.Critical, ValueStartIndex = match.Groups[1].Index, ValueLength = match.Groups[1].Length });
        }

        private void DetectAuthHeaders(string text, ConcurrentBag<PrivacyFinding> findings, PrivacyMaskingSettings settings)
        {
            if (!settings.MaskSecretsAndTokens) return;
            foreach (Match match in RxAuthHeader.Matches(text))
                findings.Add(new PrivacyFinding { Category = PrivacyCategory.AuthHeader, StartIndex = match.Index, Length = match.Length, Confidence = 0.99f, Severity = Severity.Critical, ValueStartIndex = match.Groups[1].Index, ValueLength = match.Groups[1].Length });
        }

        private void DetectJsonYamlSecrets(string text, ConcurrentBag<PrivacyFinding> findings, PrivacyMaskingSettings settings)
        {
            if (!settings.MaskPasswords && !settings.MaskSecretsAndTokens) return;
            foreach (Match match in RxJsonYamlSecret.Matches(text))
            {
                string matchStr = match.Value;
                bool isPassword = matchStr.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  matchStr.IndexOf("pwd", StringComparison.OrdinalIgnoreCase) >= 0;
                
                if (isPassword && !settings.MaskPasswords) continue;
                if (!isPassword && !settings.MaskSecretsAndTokens) continue;

                findings.Add(new PrivacyFinding { Category = PrivacyCategory.JsonSecret, StartIndex = match.Index, Length = match.Length, Confidence = 0.95f, Severity = Severity.High, ValueStartIndex = match.Groups[1].Index, ValueLength = match.Groups[1].Length });
            }
        }

        private void DetectNetworkIdentifiers(string text, ConcurrentBag<PrivacyFinding> findings, PrivacyMaskingSettings settings)
        {
            foreach (Match match in RxIpv4.Matches(text))
            {
                if (IPAddress.TryParse(match.Value, out var ip) && ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    byte[] bytes = ip.GetAddressBytes();
                    bool isPrivate = (bytes[0] == 10) || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) || (bytes[0] == 192 && bytes[1] == 168);
                    
                    if ((isPrivate && settings.MaskPrivateIp) || (!isPrivate && settings.MaskPublicIp))
                    {
                        findings.Add(new PrivacyFinding { Category = isPrivate ? PrivacyCategory.PrivateIp : PrivacyCategory.PublicIp, StartIndex = match.Index, Length = match.Length, Confidence = 0.9f, Severity = Severity.Low });
                    }
                }
            }

            foreach (Match match in RxIpv6.Matches(text))
            {
                if (IPAddress.TryParse(match.Value, out var ip) && ip.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    if (settings.MaskPublicIp)
                    {
                        findings.Add(new PrivacyFinding { Category = PrivacyCategory.Ipv6, StartIndex = match.Index, Length = match.Length, Confidence = 0.9f, Severity = Severity.Low });
                    }
                }
            }
        }

        private void DetectApiKeys(string text, ConcurrentBag<PrivacyFinding> findings, PrivacyMaskingSettings settings)
        {
            if (!settings.MaskSecretsAndTokens) return;
            foreach (Match match in RxApiKey.Matches(text))
                findings.Add(new PrivacyFinding { Category = PrivacyCategory.ApiKey, StartIndex = match.Index, Length = match.Length, Confidence = 0.99f, Severity = Severity.Critical });
        }

        private void DetectJwts(string text, ConcurrentBag<PrivacyFinding> findings, PrivacyMaskingSettings settings)
        {
            if (!settings.MaskSecretsAndTokens) return;
            foreach (Match match in RxJwt.Matches(text))
                findings.Add(new PrivacyFinding { Category = PrivacyCategory.TokenLike, StartIndex = match.Index, Length = match.Length, Confidence = 0.95f, Severity = Severity.High });
        }

        private void DetectPrivateKeys(string text, ConcurrentBag<PrivacyFinding> findings, PrivacyMaskingSettings settings)
        {
            if (!settings.MaskSecretsAndTokens) return;
            foreach (Match match in RxPrivateKey.Matches(text))
                findings.Add(new PrivacyFinding { Category = PrivacyCategory.PrivateKey, StartIndex = match.Index, Length = match.Length, Confidence = 1.0f, Severity = Severity.Critical });
        }

        private void DetectEnvironmentVariables(string text, ConcurrentBag<PrivacyFinding> findings, PrivacyMaskingSettings settings)
        {
            if (!settings.MaskPasswords && !settings.MaskSecretsAndTokens) return;
            foreach (Match match in RxEnvVar.Matches(text))
            {
                string fullMatch = match.Value;
                int eqIdx = fullMatch.IndexOf('=');
                string varName = eqIdx > 0 ? fullMatch.Substring(0, eqIdx).ToUpperInvariant() : fullMatch.ToUpperInvariant();

                bool isPassword = varName.Contains("PASSWORD") || varName.Contains("PWD");
                if (isPassword && !settings.MaskPasswords) continue;
                if (!isPassword && !settings.MaskSecretsAndTokens) continue;

                findings.Add(new PrivacyFinding { Category = PrivacyCategory.EnvironmentVariable, StartIndex = match.Index, Length = match.Length, Confidence = 0.95f, Severity = Severity.Critical, ValueStartIndex = match.Groups[1].Index, ValueLength = match.Groups[1].Length });
            }
        }

        private void DetectHashInContext(string text, ConcurrentBag<PrivacyFinding> findings, PrivacyMaskingSettings settings)
        {
            if (!settings.MaskHashIds) return;
            foreach (Match match in RxHashContext.Matches(text))
                findings.Add(new PrivacyFinding { Category = PrivacyCategory.HashContext, StartIndex = match.Index, Length = match.Length, Confidence = 0.90f, Severity = Severity.High, ValueStartIndex = match.Groups[1].Index, ValueLength = match.Groups[1].Length });
        }

        private void DetectStandaloneHashIds(string text, ConcurrentBag<PrivacyFinding> findings, PrivacyMaskingSettings settings)
        {
            if (!settings.MaskHashIds) return;
            foreach (Match match in RxStandaloneHashId.Matches(text))
            {
                findings.Add(new PrivacyFinding { Category = PrivacyCategory.HashId, StartIndex = match.Index, Length = match.Length, Confidence = 0.85f, Severity = Severity.Medium });
            }
        }

        private void DetectHostnamesAndDomains(string text, ConcurrentBag<PrivacyFinding> findings, PrivacyMaskingSettings settings)
        {
            if (!settings.MaskDomainNames) return;
            foreach (Match match in RxHostname.Matches(text))
            {
                if (match.Index > 0 && text[match.Index - 1] == '@') continue; // Part of email
                findings.Add(new PrivacyFinding { Category = PrivacyCategory.Hostname, StartIndex = match.Index, Length = match.Length, Confidence = 0.8f, Severity = Severity.Low });
            }
        }

        private void DetectEmails(string text, ConcurrentBag<PrivacyFinding> findings, PrivacyMaskingSettings settings)
        {
            if (!settings.MaskEmails) return;
            foreach (Match match in RxEmail.Matches(text))
            {
                if (match.Index > 0)
                {
                    int schemeIdx = text.LastIndexOf("://", match.Index - 1, match.Index, StringComparison.Ordinal);
                    if (schemeIdx >= 0)
                    {
                        int lastSpace = text.LastIndexOfAny(new[] { ' ', '\t', '\r', '\n' }, match.Index - 1, match.Index - schemeIdx);
                        if (lastSpace < schemeIdx) continue; // Inside URL userinfo/host
                    }
                }

                findings.Add(new PrivacyFinding { Category = PrivacyCategory.Email, StartIndex = match.Index, Length = match.Length, Confidence = 0.9f, Severity = Severity.Medium });
            }
        }

        private void DetectPhones(string text, ConcurrentBag<PrivacyFinding> findings, PrivacyMaskingSettings settings)
        {
            if (!settings.MaskPhones) return;
            foreach (Match match in RxPhone.Matches(text))
                findings.Add(new PrivacyFinding { Category = PrivacyCategory.Phone, StartIndex = match.Index, Length = match.Length, Confidence = 0.9f, Severity = Severity.Medium });
        }

        private void DetectCredentialKeyValues(string text, ConcurrentBag<PrivacyFinding> findings, PrivacyMaskingSettings settings)
        {
            foreach (Match match in RxCredentialKeyValue.Matches(text))
            {
                string keyName = match.Groups[1].Value.ToLowerInvariant();
                bool isPassword = keyName is "password" or "pwd";
                bool isSecretToken = keyName is "secret" or "token" or "key";

                if (isPassword && !settings.MaskPasswords) continue;
                if (isSecretToken && !settings.MaskSecretsAndTokens) continue;
                if (!isPassword && !isSecretToken && !settings.MaskPasswords && !settings.MaskSecretsAndTokens) continue;

                var valGroup = match.Groups[2];
                string val = valGroup.Value.TrimEnd();
                if (string.IsNullOrEmpty(val)) continue;

                bool isExplicitPassword = keyName == "password";
                float confidence = isExplicitPassword ? 0.99f : 0.90f;
                Severity severity = isExplicitPassword ? Severity.Critical : Severity.Medium;

                findings.Add(new PrivacyFinding 
                { 
                    Category = PrivacyCategory.PasswordLike, 
                    StartIndex = valGroup.Index, 
                    Length = val.Length, 
                    Confidence = confidence, 
                    Severity = severity, 
                    ValueStartIndex = valGroup.Index, 
                    ValueLength = val.Length 
                });
            }
        }

        private void DetectCustomBlockedPatterns(string text, ConcurrentBag<PrivacyFinding> findings, PrivacyMaskingSettings settings)
        {
            if (settings.CustomBlockedPatterns == null || settings.CustomBlockedPatterns.Count == 0) return;

            foreach (var pattern in settings.CustomBlockedPatterns)
            {
                if (string.IsNullOrWhiteSpace(pattern)) continue;
                try
                {
                    var rx = new Regex(pattern, RegexOptions.IgnoreCase);
                    foreach (Match match in rx.Matches(text))
                    {
                        findings.Add(new PrivacyFinding
                        {
                            Category = PrivacyCategory.SecretLike,
                            StartIndex = match.Index,
                            Length = match.Length,
                            Confidence = 0.99f,
                            Severity = Severity.High,
                            Source = FindingSource.CustomRule
                        });
                    }
                }
                catch
                {
                    // Ignore invalid user regex
                }
            }
        }

        private bool IsCredentialBearingCategory(PrivacyCategory category)
        {
            return category is PrivacyCategory.PasswordLike 
                or PrivacyCategory.DatabaseCredential 
                or PrivacyCategory.ApiKey 
                or PrivacyCategory.PrivateKey 
                or PrivacyCategory.AuthHeader 
                or PrivacyCategory.TokenLike 
                or PrivacyCategory.JsonSecret 
                or PrivacyCategory.EnvironmentVariable 
                or PrivacyCategory.CloudCredential 
                or PrivacyCategory.HashContext;
        }

        private ClassificationResult EvaluatePolicyAndBuildPlan(string rawText, List<PrivacyFinding> findings, PrivacyMaskingSettings settings)
        {
            var rawMaskSpans = new List<MaskSpan>();
            bool isSensitive = false;
            Severity effectiveSeverity = Severity.Low;

            var credentials = findings.Where(f => IsCredentialBearingCategory(f.Category) && f.Confidence >= ClassifyPolicy.ConfidenceThreshold).ToList();
            var contextuals = findings.Where(f => !IsCredentialBearingCategory(f.Category)).ToList();
            
            foreach (var cred in credentials)
            {
                isSensitive = true;
                if (cred.Severity > effectiveSeverity) effectiveSeverity = cred.Severity;

                int start = cred.ValueStartIndex ?? cred.StartIndex;
                int len = cred.ValueLength ?? cred.Length;
                
                var mode = (cred.Severity == Severity.Critical || cred.Confidence >= 0.99f) ? MaskingMode.Full : DetermineMaskingMode(cred.Category);
                if (mode != MaskingMode.Preserve)
                {
                    var span = MaskSpan.TryCreate(start, len, mode, rawText.Length);
                    if (span != null) rawMaskSpans.Add(span);
                }
            }

            foreach (var finding in contextuals)
            {
                if (finding.Confidence < ClassifyPolicy.ConfidenceThreshold) continue;

                if (finding.Category == PrivacyCategory.Email || 
                    finding.Category == PrivacyCategory.Phone || 
                    finding.Category == PrivacyCategory.PrivateIp || 
                    finding.Category == PrivacyCategory.PublicIp ||
                    finding.Category == PrivacyCategory.Ipv6 ||
                    finding.Category == PrivacyCategory.Hostname ||
                    finding.Category == PrivacyCategory.Port ||
                    finding.Category == PrivacyCategory.DatabaseName ||
                    finding.Category == PrivacyCategory.HashId ||
                    finding.Category == PrivacyCategory.SecretLike)
                {
                    isSensitive = true;
                    if (finding.Severity > effectiveSeverity) effectiveSeverity = finding.Severity;
                    
                    var mode = (finding.Category == PrivacyCategory.Port || 
                                finding.Category == PrivacyCategory.DatabaseName || 
                                finding.Category == PrivacyCategory.HashId ||
                                finding.Category == PrivacyCategory.SecretLike) ? MaskingMode.Full : MaskingMode.Partial;

                    var span = MaskSpan.TryCreate(finding.StartIndex, finding.Length, mode, rawText.Length);
                    if (span != null) rawMaskSpans.Add(span);
                }
                else if (finding.Category == PrivacyCategory.Username && isSensitive)
                {
                    var span = MaskSpan.TryCreate(finding.StartIndex, finding.Length, MaskingMode.Full, rawText.Length);
                    if (span != null) rawMaskSpans.Add(span);
                }
            }

            var consolidatedPlan = ResolveMaskingOverlaps(rawMaskSpans, rawText.Length);

            return new ClassificationResult
            {
                IsSensitive = isSensitive && consolidatedPlan.Count > 0,
                OverallConfidence = credentials.Any() ? credentials.Max(c => c.Confidence) : (contextuals.Any() ? 0.5f : 0f), 
                EffectiveSeverity = effectiveSeverity,
                Findings = findings,
                MaskingPlan = consolidatedPlan
            };
        }

        private MaskingMode DetermineMaskingMode(PrivacyCategory category)
        {
            return category switch
            {
                PrivacyCategory.Email => MaskingMode.Partial,
                PrivacyCategory.Phone => MaskingMode.Partial,
                PrivacyCategory.PrivateIp => MaskingMode.Partial,
                PrivacyCategory.PublicIp => MaskingMode.Partial,
                PrivacyCategory.Ipv6 => MaskingMode.Partial,
                PrivacyCategory.Hostname => MaskingMode.Partial,
                PrivacyCategory.PasswordLike => MaskingMode.Partial,
                PrivacyCategory.DatabaseCredential => MaskingMode.Full,
                PrivacyCategory.Username => MaskingMode.Full,
                PrivacyCategory.ApiKey => MaskingMode.Full,
                PrivacyCategory.PrivateKey => MaskingMode.Full,
                PrivacyCategory.AuthHeader => MaskingMode.Full,
                PrivacyCategory.TokenLike => MaskingMode.Full,
                PrivacyCategory.JsonSecret => MaskingMode.Full,
                PrivacyCategory.EnvironmentVariable => MaskingMode.Full,
                PrivacyCategory.HashContext => MaskingMode.Full,
                PrivacyCategory.HashId => MaskingMode.Full,
                PrivacyCategory.CloudCredential => MaskingMode.Full,
                PrivacyCategory.Port => MaskingMode.Full,
                PrivacyCategory.DatabaseName => MaskingMode.Full,
                _ => MaskingMode.Preserve
            };
        }

        private IReadOnlyList<MaskSpan> ResolveMaskingOverlaps(List<MaskSpan> spans, int maxTextLength)
        {
            if (spans.Count <= 1) return spans;

            var sorted = spans.OrderBy(s => s.Start).ThenByDescending(s => s.Length).ToList();
            var merged = new List<MaskSpan>();

            MaskSpan current = sorted[0];
            for (int i = 1; i < sorted.Count; i++)
            {
                var next = sorted[i];
                if (current.Start + current.Length >= next.Start) 
                {
                    int newEnd = Math.Max(current.Start + current.Length, next.Start + next.Length);
                    var newMode = (current.Mode == MaskingMode.Full || next.Mode == MaskingMode.Full) ? MaskingMode.Full : MaskingMode.Partial;
                    
                    var mergedSpan = MaskSpan.TryCreate(current.Start, newEnd - current.Start, newMode, maxTextLength);
                    if (mergedSpan != null) current = mergedSpan;
                }
                else
                {
                    merged.Add(current);
                    current = next;
                }
            }
            merged.Add(current);
            return merged;
        }
    }
}

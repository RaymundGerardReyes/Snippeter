using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ClipboardManager.Models.Ml;

namespace ClipboardManager.Services.Ml
{
    public class MlModelLoader : IMlModelLoader
    {
        public const int SupportedModelMajorVersion = 1;
        public const double MinimumAcceptableRecall = 0.85;

        private readonly string _modelsBasePath;

        public bool IsModelLoaded { get; private set; }
        public ModelLoadStatus Status { get; private set; } = ModelLoadStatus.NotAttempted;
        public ModelManifest? Manifest { get; private set; }

        public MlModelLoader(string modelsBasePath)
        {
            _modelsBasePath = modelsBasePath;
        }

        public async Task LoadAsync(CancellationToken cancellationToken)
        {
            try
            {
                var pointerPath = Path.Combine(_modelsBasePath, "current.json");
                if (!File.Exists(pointerPath))
                {
                    Fail(ModelLoadStatus.MissingFile);
                    return;
                }

                var pointerJson = await File.ReadAllTextAsync(pointerPath, cancellationToken);
                using var pointerDoc = JsonDocument.Parse(pointerJson);
                var activeVersion = pointerDoc.RootElement.GetProperty("active_version").GetString();

                if (string.IsNullOrWhiteSpace(activeVersion))
                {
                    Fail(ModelLoadStatus.MissingFile);
                    return;
                }

                var versionDir = Path.Combine(_modelsBasePath, "versions", activeVersion);
                var manifestPath = Path.Combine(versionDir, "model.manifest.json");
                var onnxPath = Path.Combine(versionDir, "secret_pii_detector.onnx");

                if (!File.Exists(manifestPath) || !File.Exists(onnxPath))
                {
                    Fail(ModelLoadStatus.MissingFile);
                    return;
                }

                var manifestJson = await File.ReadAllTextAsync(manifestPath, cancellationToken);
                ModelManifest? manifest;
                try
                {
                    manifest = JsonSerializer.Deserialize<ModelManifest>(manifestJson);
                    if (manifest == null)
                    {
                        Fail(ModelLoadStatus.LoadError);
                        return;
                    }
                }
                catch (JsonException)
                {
                    Fail(ModelLoadStatus.LoadError);
                    return;
                }

                var actualSha = await ComputeSha256Async(onnxPath, cancellationToken);
                if (!string.Equals(actualSha, manifest.ContentSha256, StringComparison.OrdinalIgnoreCase))
                {
                    Fail(ModelLoadStatus.HashMismatch);
                    return;
                }

                if (string.IsNullOrWhiteSpace(manifest.Semver))
                {
                    Fail(ModelLoadStatus.LoadError);
                    return;
                }

                var semverParts = manifest.Semver.Split('.');
                if (semverParts.Length == 0 || !int.TryParse(semverParts[0], out int modelMajor))
                {
                    Fail(ModelLoadStatus.LoadError);
                    return;
                }

                if (modelMajor != SupportedModelMajorVersion)
                {
                    Fail(ModelLoadStatus.MajorVersionMismatch);
                    return;
                }

                if (manifest.Evaluation.SpanRecallOverall < MinimumAcceptableRecall)
                {
                    Fail(ModelLoadStatus.RecallBelowThreshold);
                    return;
                }

                IsModelLoaded = true;
                Status = ModelLoadStatus.Loaded;
                Manifest = manifest;
            }
            catch (Exception)
            {
                Fail(ModelLoadStatus.LoadError);
            }
        }

        private void Fail(ModelLoadStatus status)
        {
            IsModelLoaded = false;
            Status = status;
            Manifest = null;
        }

        private async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = await sha256.ComputeHashAsync(stream, cancellationToken);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
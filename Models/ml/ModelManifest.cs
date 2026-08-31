using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ClipboardManager.Models.Ml
{
    public class ModelManifest
    {
        [JsonPropertyName("model_name")]
        public string ModelName { get; set; } = string.Empty;

        [JsonPropertyName("semver")]
        public string Semver { get; set; } = string.Empty;

        [JsonPropertyName("content_sha256")]
        public string ContentSha256 { get; set; } = string.Empty;

        [JsonPropertyName("onnx_ir_version")]
        public int OnnxIrVersion { get; set; }

        [JsonPropertyName("opset_version")]
        public int OpsetVersion { get; set; }

        [JsonPropertyName("label_schema_version")]
        public int LabelSchemaVersion { get; set; }

        [JsonPropertyName("labels")]
        public List<string> Labels { get; set; } = new List<string>();

        [JsonPropertyName("tokenizer")]
        public ManifestTokenizer Tokenizer { get; set; } = new ManifestTokenizer();

        [JsonPropertyName("dataset_snapshot")]
        public string DatasetSnapshot { get; set; } = string.Empty;

        [JsonPropertyName("training_config_hash")]
        public string TrainingConfigHash { get; set; } = string.Empty;

        [JsonPropertyName("git_commit")]
        public string GitCommit { get; set; } = string.Empty;

        [JsonPropertyName("exported_at_utc")]
        public DateTime ExportedAtUtc { get; set; }

        [JsonPropertyName("evaluation")]
        public ManifestEvaluation Evaluation { get; set; } = new ManifestEvaluation();

        [JsonPropertyName("min_app_major_version")]
        public int MinAppMajorVersion { get; set; }
    }

    public class ManifestTokenizer
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("vocab_file")]
        public string VocabFile { get; set; } = string.Empty;

        [JsonPropertyName("max_sequence_length")]
        public int MaxSequenceLength { get; set; }
    }

    public class ManifestEvaluation
    {
        [JsonPropertyName("span_f1_overall")]
        public double SpanF1Overall { get; set; }

        [JsonPropertyName("span_recall_overall")]
        public double SpanRecallOverall { get; set; }

        [JsonPropertyName("span_precision_overall")]
        public double SpanPrecisionOverall { get; set; }

        [JsonPropertyName("per_category")]
        public Dictionary<string, CategoryMetrics> PerCategory { get; set; } = new Dictionary<string, CategoryMetrics>();
    }

    public class CategoryMetrics
    {
        [JsonPropertyName("precision")]
        public double Precision { get; set; }

        [JsonPropertyName("recall")]
        public double Recall { get; set; }

        [JsonPropertyName("f1")]
        public double F1 { get; set; }
    }
}

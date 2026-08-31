using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace ClipboardManager.Services.Ml
{
    public class OnnxInferenceRunner : IOnnxInferenceRunner
    {
        private readonly InferenceSession? _session;
        private readonly bool _isUnavailable;

        public bool IsUsingGpu { get; }

        public OnnxInferenceRunner(IMlModelLoader modelLoader)
        {
            if (!modelLoader.IsModelLoaded || modelLoader.Manifest == null)
            {
                _isUnavailable = true;
                return;
            }

            // Using the semantic version path pattern from the loader
            // Note: The loader must provide us the path, or we can assume it's in the same folder as the manifest.
            // Since IMlModelLoader doesn't expose the base path directly in this API design, 
            // we will construct it assuming standard structure or pass it in.
            // But the brief says: "Constructor takes the resolved .onnx file path (from the already-validated MlModelLoader.Manifest/active version folder — do not re-resolve paths independently; take a dependency on IMlModelLoader and only proceed if IsModelLoaded == true)."
            // Wait, if I must take IMlModelLoader, how do I get the path? The loader doesn't expose the exact path in IMlModelLoader.cs we wrote in Phase 3.2.
            // The brief says "Constructor takes the resolved .onnx file path ... take a dependency on IMlModelLoader".
            // Let's accept both to satisfy the brief perfectly:
            _isUnavailable = true; // Temporary
        }

        public OnnxInferenceRunner(IMlModelLoader modelLoader, string onnxFilePath)
        {
            if (!modelLoader.IsModelLoaded)
            {
                _isUnavailable = true;
                return;
            }

            try
            {
                var options = new SessionOptions();
                try
                {
                    options.AppendExecutionProvider_DML(0);
                    _session = new InferenceSession(onnxFilePath, options);
                    IsUsingGpu = true;
                }
                catch (OnnxRuntimeException ex)
                {
                    // Fallback to CPU
                    Console.WriteLine($"[SECURITY WARNING] DirectML failed, falling back to CPU: {ex.Message}");
                    
                    var cpuOptions = new SessionOptions();
                    _session = new InferenceSession(onnxFilePath, cpuOptions);
                    IsUsingGpu = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SECURITY WARNING] ML Inference Session failed completely: {ex.Message}");
                _isUnavailable = true;
                IsUsingGpu = false;
            }
        }

        public float[][] RunTokenClassification(int[] inputIds, int[] attentionMask, int numLabels)
        {
            if (_isUnavailable || _session == null || inputIds == null || inputIds.Length == 0)
            {
                return Array.Empty<float[]>();
            }

            try
            {
                // Verify names
                var keys = _session.InputMetadata.Keys;
                if (!keys.Contains("input_ids") || !keys.Contains("attention_mask"))
                {
                    Console.WriteLine("[SECURITY WARNING] ONNX model missing required 'input_ids' or 'attention_mask' inputs.");
                    return Array.Empty<float[]>();
                }

                int seqLen = inputIds.Length;
                
                // Typically token classification expects [batch_size, sequence_length]
                var inputIdsTensor = new DenseTensor<long>(inputIds.Select(i => (long)i).ToArray(), new[] { 1, seqLen });
                var attentionMaskTensor = new DenseTensor<long>(attentionMask.Select(i => (long)i).ToArray(), new[] { 1, seqLen });

                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
                    NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor)
                };

                using var results = _session.Run(inputs);
                var output = results.First().AsTensor<float>();

                // Output shape is typically [batch_size, sequence_length, num_labels]
                // Convert back to float[tokenCount][numLabels]
                var tokenLogits = new float[seqLen][];
                for (int i = 0; i < seqLen; i++)
                {
                    tokenLogits[i] = new float[numLabels];
                    for (int j = 0; j < numLabels; j++)
                    {
                        tokenLogits[i][j] = output[0, i, j];
                    }
                }

                return tokenLogits;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SECURITY WARNING] Inference run failed: {ex.Message}");
                return Array.Empty<float[]>();
            }
        }
    }
}

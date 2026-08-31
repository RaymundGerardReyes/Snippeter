namespace ClipboardManager.Services.Ml
{
    public interface IOnnxInferenceRunner
    {
        bool IsUsingGpu { get; }
        float[][] RunTokenClassification(int[] inputIds, int[] attentionMask, int numLabels);
    }
}

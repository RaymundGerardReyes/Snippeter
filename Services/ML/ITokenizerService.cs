namespace ClipboardManager.Services.Ml
{
    public interface ITokenizerService
    {
        bool IsReady { get; }
        (int[] InputIds, int[] AttentionMask, (int Start, int Length)[] Offsets) Tokenize(string text, int maxSequenceLength);
    }
}

namespace ClipboardManager.Models.Ml
{
    public enum ModelLoadStatus
    {
        NotAttempted,
        Loaded,
        MissingFile,
        HashMismatch,
        MajorVersionMismatch,
        RecallBelowThreshold,
        LoadError
    }
}

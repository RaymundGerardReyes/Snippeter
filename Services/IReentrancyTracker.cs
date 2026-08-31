using System;

namespace ClipboardManager.Services
{
    public interface IReentrancyTracker
    {
        void RegisterProgrammaticWrite(string expectedText);
        void CancelProgrammaticWrite(string expectedText);
        bool ConsumeExpectedWrite(string rawText);
    }
}

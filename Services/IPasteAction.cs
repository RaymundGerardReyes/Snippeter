namespace ClipboardManager.Services
{
    public interface IPasteAction
    {
        void SimulatePaste();
    }

    public class Win32PasteAction : IPasteAction
    {
        public void SimulatePaste()
        {
            Helpers.NativeMethods.SimulatePaste();
        }
    }
}

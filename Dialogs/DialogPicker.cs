using Microsoft.UI.Reactor;

namespace BitwardenForReactor.Dialogs;

internal static class DialogPicker
{
    public static void Initialize(object picker)
    {
        var window = ReactorApp.PrimaryWindow?.Host.Window;
        if (window is null)
        {
            return;
        }

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
    }
}

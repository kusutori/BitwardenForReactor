using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace BitwardenForReactor.Services;

public static class ClipboardService
{
    public static void CopyToClipboard(string text)
    {
        var dataPackage = new DataPackage();
        dataPackage.SetText(text);
        Clipboard.SetContent(dataPackage);
    }

    public static Task CopyToClipboardWithTimeoutAsync(string text, int timeoutSeconds)
    {
        CopyToClipboard(text);

        if (timeoutSeconds <= 0)
        {
            return Task.CompletedTask;
        }

        _ = ClearAfterDelayAsync(timeoutSeconds);
        return Task.CompletedTask;
    }

    private static async Task ClearAfterDelayAsync(int timeoutSeconds)
    {
        await Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
        Clipboard.Clear();
    }
}

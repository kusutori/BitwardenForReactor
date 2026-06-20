using BitwardenForReactor.Models;
using BitwardenForReactor.Services;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using static Microsoft.UI.Reactor.Factories;

namespace BitwardenForReactor.Components;

public sealed record ItemIconProps(BitwardenItem Item, double Size);

public sealed class ItemIcon : Component<ItemIconProps>
{
    public override Element Render()
    {
        var (loadedUrl, setLoadedUrl) = UseState<string?>(null);
        var (failedUrl, setFailedUrl) = UseState<string?>(null);
        var icon = IconService.GetItemIcon(Props.Item);
        var fallback = Icon(FontIcon(IconService.GetItemTypeGlyph(Props.Item.Type), fontSize: Props.Size));
        if (string.IsNullOrWhiteSpace(icon.ImageUrl))
        {
            return fallback;
        }

        var imageUrl = icon.ImageUrl;
        if (failedUrl == imageUrl)
        {
            return fallback;
        }

        return Grid(
            columns: [GridSize.Star()],
            rows: [GridSize.Star()],
            fallback.IsVisible(loadedUrl != imageUrl),
            Image(imageUrl)
                .ImageOpened(() => setLoadedUrl(imageUrl))
                .ImageFailed(_ => setFailedUrl(imageUrl))
                .Width(Props.Size)
                .Height(Props.Size)
                .Opacity(loadedUrl == imageUrl ? 1 : 0)
                .AccessibilityHidden());
    }
}

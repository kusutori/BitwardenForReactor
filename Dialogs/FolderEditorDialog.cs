using System;
using BitwardenForReactor.Application;
using BitwardenForReactor.State;
using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using static Microsoft.UI.Reactor.Factories;

namespace BitwardenForReactor.Dialogs;

public sealed record FolderEditorDialogProps(Action<AppAction> Dispatch);

public sealed class FolderEditorDialog : Component<FolderEditorDialogProps>
{
    public override Element Render()
    {
        var (name, setName) = UseState(string.Empty);

        void Save()
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                _ = AppCommands.CreateFolderAsync(name, Props.Dispatch);
            }
        }

        return Border(
                ModalCard(
                    Border(
                            Grid(
                                columns: [GridSize.Star()],
                                rows: [GridSize.Auto, GridSize.Auto, GridSize.Auto],
                                Heading("新建文件夹")
                                    .Margin(left: 20, top: 18, right: 20, bottom: 10)
                                    .Grid(row: 0),
                                VStack(10,
                                    TextBlock("输入名称后会在当前密码库中创建文件夹。")
                                        .Foreground(Theme.SecondaryText)
                                        .TextWrapping(),
                                    TextBox(name, setName, header: "文件夹名称")
                                        .AutomationName("文件夹名称")
                                        .OnKeyDown((_, e) =>
                                        {
                                            if (e.Key == VirtualKey.Enter)
                                            {
                                                Save();
                                                e.Handled = true;
                                            }
                                        }))
                                    .Padding(left: 20, top: 4, right: 20, bottom: 18)
                                    .Grid(row: 1),
                                Border(
                                        HStack(12,
                                            Button("取消", () => Props.Dispatch(new FolderEditorVisibilityChanged(false)))
                                                .MinWidth(96)
                                                .AutomationName("取消创建文件夹"),
                                            Button("创建", Save)
                                                .AccentButton()
                                                .MinWidth(96)
                                                .IsEnabled(!string.IsNullOrWhiteSpace(name))
                                                .AutomationName("创建文件夹"))
                                            .HorizontalAlignment(HorizontalAlignment.Right))
                                    .WithBorder(Theme.CardStroke, 1)
                                    .Padding(16)
                                    .Grid(row: 2)))
                        .Background(Theme.SolidBackground)
                        .WithBorder(Theme.CardStroke, 1)
                        .CornerRadius(8)
                        .Width(420)
                        .HorizontalAlignment(HorizontalAlignment.Center)
                        .VerticalAlignment(VerticalAlignment.Center)
                        .AutomationName("文件夹编辑器"),
                    420))
            .Background(Theme.SmokeFill)
            .AutomationName("文件夹编辑器遮罩");
    }

    private static Element ModalCard(Element card, double width) =>
        Grid(
                columns: [GridSize.Star()],
                rows: [GridSize.Star()],
                Border(VStack())
                    .Background(Theme.SystemNeutral)
                    .Opacity(0.16)
                    .CornerRadius(10)
                    .Width(width)
                    .Margin(left: 6, top: 8, right: 0, bottom: 0)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .VerticalAlignment(VerticalAlignment.Stretch),
                card)
            .HorizontalAlignment(HorizontalAlignment.Center)
            .VerticalAlignment(VerticalAlignment.Center);
}

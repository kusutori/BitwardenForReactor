using BitwardenForReactor;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Reactor;

ReactorApp.RegisterControlAssembly(typeof(SettingsCard).Assembly);
ReactorApp.RegisterControlAssembly(typeof(Segmented).Assembly);
ReactorApp.Run<App>("BitwardenForReactor", width: 1120, height: 720);

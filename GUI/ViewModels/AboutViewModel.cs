using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace GUI.ViewModels;

internal class AboutViewModel {
    public string Title => AppInfo.Name;
    public string Version => AppInfo.VersionString;
    public string MoreInfoUrl => "https://github.com/md8n/GCodeClean";
    public string Message => "This app is written in XAML and C# with .NET MAUI.";
    public ICommand ShowMoreInfoCommand { get; }

    public AboutViewModel() {
        ShowMoreInfoCommand = new AsyncRelayCommand(ShowMoreInfo);
    }

    async Task ShowMoreInfo() =>
        await Launcher.Default.OpenAsync(MoreInfoUrl);
}
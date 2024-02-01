using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Actions.Merge;

namespace GUI.ViewModels;

internal class MergeViewModel : ObservableObject {
    private Models.Merge _merge;
    private string[] _logging;
    private readonly object _loggingLock = new object();

    public string Source {
        get => _merge.Folder;
    }

    public string Logging {
        get => string.Join(Environment.NewLine, _logging);
        set {
            lock (_loggingLock) {
                if (string.IsNullOrWhiteSpace(value)) {
                    _logging = [];
                } else {
                    _logging = _logging.Where(l => l != value).Append(value).ToArray();
                }
            }
        }
    }

    public AsyncRelayCommand SelectCommand { get; private set; }
    public AsyncRelayCommand MergeCommand { get; private set; }

    public MergeViewModel() {
        _merge = new Models.Merge {
            Folder = ""
        };
        _logging = [];
        SelectCommand = new AsyncRelayCommand(Select);
        MergeCommand = new AsyncRelayCommand(Merge, CanMerge);
    }

    private async Task Select() {
        var result = await FolderPicker.Default.PickAsync();
        if (!result.IsSuccessful) {
            DoLogging("Folder selection failed");
            RefreshProperties();
            return;
        }
        var folder = result.Folder.Path;
        _merge = Models.Merge.Load(folder);
        RefreshProperties();
    }

    private bool CanMerge() {
        return !string.IsNullOrWhiteSpace(_merge.Folder);
    }

    private void DoLogging(string logMessage) {
        Logging = logMessage;
        OnPropertyChanged(nameof(Logging));
    }

    private async Task Merge() {
        string lastMessage = "";
        DoLogging("Starting");
        await Task.Run(async () => {
            await foreach (string logMessage in MergeAction.ExecuteAsync(new DirectoryInfo(_merge.Folder))) {
                DoLogging(logMessage);
                lastMessage = logMessage;
            }
        });
        var result = lastMessage == "Success" ? 0 : 1;
        DoLogging(lastMessage);
        await Shell.Current.GoToAsync($"..?merge={_merge.Folder}");
    }

    private void RefreshProperties() {
        MergeCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(Source));
    }
}

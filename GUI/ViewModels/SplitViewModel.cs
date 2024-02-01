using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Actions.Split;

namespace GUI.ViewModels;

internal class SplitViewModel : ObservableObject {
    private Models.Split _split;
    private string[] _logging;

    public string Source {
        get => _split.Filename;
    }

    public string Logging {
        get => string.Join(Environment.NewLine, _logging);
        set {
            if (string.IsNullOrWhiteSpace(value)) {
                _logging = [];
            } else {
                _logging = _logging.Where(l => l != value).Append(value).ToArray();
            }
        }
    }

    public AsyncRelayCommand SelectCommand { get; private set; }
    public AsyncRelayCommand SplitCommand { get; private set; }

    public SplitViewModel() {
        _split = new Models.Split {
            Filename = ""
        };
        _logging = [];
        SelectCommand = new AsyncRelayCommand(Select);
        SplitCommand = new AsyncRelayCommand(Split, CanSplit);
    }

    private async Task Select() {
        var file = await FilePicker.Default.PickAsync(new PickOptions {
            PickerTitle = "Select GCode File"
        });
        if (file == null) {
            RefreshProperties();
            return;
        }
        var fileSource = file.FullPath;
        if (string.IsNullOrWhiteSpace(fileSource)) {
            RefreshProperties();
            return;
        }
        _split = Models.Split.Load(fileSource);
        RefreshProperties();
    }

    private bool CanSplit() {
        return !string.IsNullOrWhiteSpace(_split.Filename);
    }

    private void DoLogging(string logMessage) {
        Logging = logMessage;
        OnPropertyChanged(nameof(Logging));
    }

    private async Task Split() {
        string lastMessage = "";
        DoLogging("Starting");
        await foreach (string logMessage in SplitAction.ExecuteAsync(new FileInfo(_split.Filename))) {
            //DoLogging(logMessage);
            lastMessage = logMessage;
        }
        var result = lastMessage == "Success" ? 0 : 1;
        DoLogging(lastMessage);
        await Shell.Current.GoToAsync($"..?split={_split.Filename}");
    }

    private void RefreshProperties() {
        SplitCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(Source));
    }
}

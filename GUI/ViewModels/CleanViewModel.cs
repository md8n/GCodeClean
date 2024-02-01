using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Actions.Clean;

namespace GUI.ViewModels;

internal class CleanViewModel : ObservableObject {
    private Models.Clean _clean;
    private string[] _logging;

    public string Source {
        get => _clean.Filename;
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
    public AsyncRelayCommand CleanCommand { get; private set; }

    public CleanViewModel() {
        _clean = new Models.Clean {
            Filename = ""
        };
        _logging = [];
        SelectCommand = new AsyncRelayCommand(Select);
        CleanCommand = new AsyncRelayCommand(Clean, CanClean);
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
        _clean = Models.Clean.Load(fileSource);
        RefreshProperties();
    }

    private bool CanClean() {
        return !string.IsNullOrWhiteSpace(_clean.Filename);
    }

    private void DoLogging(string logMessage) {
        Logging = logMessage;
        RefreshProperties();
    }

    private async Task Clean() {
        var fileInfo = new FileInfo(_clean.Filename);
        var annotate = false;
        var lineNumbers = false;
        var minimise = "";
        decimal tolerance = 0M;
        decimal arcTolerance = 0M;
        decimal zClamp = 0M;
        FileInfo tokenDefs = new FileInfo("tokenDefinitions.json");
        var (tokenDefinitions, _) = tokenDefs.LoadAndVerifyTokenDefs();

        var result = await CleanAction.ExecuteAsync(fileInfo, annotate, lineNumbers, minimise, tolerance, arcTolerance, zClamp, tokenDefinitions, DoLogging);
        await Shell.Current.GoToAsync($"..?clean={_clean.Filename}");
    }

    private void RefreshProperties() {
        CleanCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(Source));
        OnPropertyChanged(nameof(Logging));
    }
}

namespace GUI.Models;

internal class Merge {
    public string Folder { get; set; }

    public Merge() {
        Folder = "";
    }

    public static Merge Load(string folder) {
        if (!Directory.Exists(folder) || string.IsNullOrWhiteSpace(folder))
            throw new DirectoryNotFoundException($"Unable to find folder on local storage. '{folder}'");

        return new() { Folder = folder };
    }
}

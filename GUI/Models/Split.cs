namespace GUI.Models;

internal class Split {
    public string Filename { get; set; }

    public Split() {
        Filename = "";
    }

    public static Split Load(string filename) {
        if (!File.Exists(filename))
            throw new FileNotFoundException("Unable to find file on local storage.", filename);

        return new() { Filename = Path.GetFullPath(filename)};
    }
}

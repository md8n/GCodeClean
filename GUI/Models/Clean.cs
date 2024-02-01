namespace GUI.Models;

internal class Clean {
    public string Filename { get; set; }

    public Clean() {
        Filename = "";
    }

    public static Clean Load(string filename) {
        if (!File.Exists(filename))
            throw new FileNotFoundException("Unable to find file on local storage.", filename);

        return new() { Filename = Path.GetFullPath(filename)};
    }
}

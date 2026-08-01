namespace OliveGameStudio.Localisation.Tests;

/// <summary>
/// A throwaway folder of language files, so a test can describe the exact set of translations it is
/// about and clean them up afterwards.
/// </summary>
sealed class TextFolder : IDisposable
{
    /// <summary>
    /// Creates the folder and writes a file per language.
    /// </summary>
    /// <param name="files">The language name and the JSON to write for it.</param>
    public TextFolder(params (string Language, string Json)[] files)
    {
        FullPath = Path.Combine(Path.GetTempPath(), $"olive-text-{Guid.NewGuid():N}");
        Directory.CreateDirectory(FullPath);

        foreach ((string language, string json) in files)
        {
            Write(language, json);
        }
    }

    /// <summary>
    /// Gets the folder the language files are in.
    /// </summary>
    public string FullPath { get; }

    /// <summary>
    /// Writes one language file, as a translator dropping a file in would.
    /// </summary>
    /// <param name="language">The language name, which is the file name.</param>
    /// <param name="json">The file's contents.</param>
    public void Write(string language, string json) =>
        File.WriteAllText(Path.Combine(FullPath, $"{language}.json"), json);

    /// <summary>
    /// Reads the folder.
    /// </summary>
    /// <returns>A provider over the files written so far.</returns>
    public JsonTextProvider Read() => new(FullPath);

    /// <summary>
    /// Deletes the folder.
    /// </summary>
    public void Dispose()
    {
        try
        {
            Directory.Delete(FullPath, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
            // a test that deleted it itself is not a failure
        }
    }
}

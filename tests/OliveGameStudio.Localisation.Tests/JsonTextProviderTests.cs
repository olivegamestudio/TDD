using System.Globalization;

namespace OliveGameStudio.Localisation.Tests;

/// <summary>
/// Covers reading translated text out of a folder of JSON language files: which language a look-up
/// lands in, what it falls back to, and how a broken or missing file fails.
/// </summary>
public sealed class JsonTextProviderTests
{
    const string English = """{ "Greeting": "Get out.", "Farewell": "Good luck." }""";
    const string French = """{ "Greeting": "Sors de là.", "Farewell": "Bonne chance." }""";

    static TextFolder TwoLanguages() => new(("en", English), ("fr", French));

    [Fact]
    public void ReadsTheSourceLanguage()
    {
        using TextFolder folder = TwoLanguages();

        Assert.Equal("Get out.", folder.Read().Get("Greeting", new CultureInfo("en")));
    }

    [Fact]
    public void ReadsATranslation()
    {
        using TextFolder folder = TwoLanguages();

        Assert.Equal("Sors de là.", folder.Read().Get("Greeting", new CultureInfo("fr")));
    }

    [Fact]
    public void ReadsThePlayersCurrentLanguage()
    {
        using TextFolder folder = TwoLanguages();
        JsonTextProvider text = folder.Read();

        using CultureScope _ = new("fr");

        Assert.Equal("Sors de là.", text.Get("Greeting"));
    }

    [Fact]
    public void ARegionalLanguage_FallsBackToItsBaseLanguage()
    {
        // fr-CA has no file of its own, so it reads the French one
        using TextFolder folder = TwoLanguages();

        Assert.Equal("Sors de là.", folder.Read().Get("Greeting", new CultureInfo("fr-CA")));
    }

    [Fact]
    public void AnUntranslatedLanguage_FallsBackToTheSourceLanguage()
    {
        // Dutch has no translation, so the player reads English rather than a blank or a key
        using TextFolder folder = TwoLanguages();

        Assert.Equal("Get out.", folder.Read().Get("Greeting", new CultureInfo("nl")));
    }

    [Fact]
    public void AHalfTranslatedLanguage_FallsBackKeyByKey()
    {
        using TextFolder folder = new(("en", English), ("de", """{ "Greeting": "Raus hier." }"""));
        JsonTextProvider text = folder.Read();
        CultureInfo german = new("de");

        Assert.Equal("Raus hier.", text.Get("Greeting", german));
        Assert.Equal("Good luck.", text.Get("Farewell", german));
    }

    [Fact]
    public void ALanguageDroppedIntoTheFolder_NeedsNoRebuild()
    {
        using TextFolder folder = TwoLanguages();
        folder.Write("it", """{ "Greeting": "Esci di qui." }""");

        Assert.Equal("Esci di qui.", folder.Read().Get("Greeting", new CultureInfo("it")));
    }

    [Fact]
    public void ARegionalFileWinsOverItsBaseLanguage()
    {
        using TextFolder folder = new(
            ("en", English),
            ("pt", """{ "Greeting": "Sai daí." }"""),
            ("pt-BR", """{ "Greeting": "Saia daí." }"""));

        Assert.Equal("Saia daí.", folder.Read().Get("Greeting", new CultureInfo("pt-BR")));
    }

    [Fact]
    public void Get_ThrowsForAKeyThatExistsInNoLanguage()
    {
        using TextFolder folder = TwoLanguages();

        MissingTextException exception =
            Assert.Throws<MissingTextException>(() => folder.Read().Get("not-a-key"));

        Assert.Equal("not-a-key", exception.Key);
    }

    [Fact]
    public void TheSourceFile_MayCarryNotesForTranslators()
    {
        // JSON has nowhere else to explain the context of a string, and a translator working in a
        // text editor needs it
        using TextFolder folder = new(
            ("en", """
                   {
                     // said over the radio, urgently
                     "Greeting": "Get out.",
                   }
                   """));

        Assert.Equal("Get out.", folder.Read().Get("Greeting", new CultureInfo("en")));
    }

    [Fact]
    public void Languages_ListsEveryFileFound()
    {
        using TextFolder folder = TwoLanguages();

        Assert.Equal(new[] { "en", "fr" }, folder.Read().Languages.OrderBy(language => language));
    }

    [Fact]
    public void TextIn_ReadsOneLanguageWithNoFallback()
    {
        // a language that is missing a key has to read as missing, not borrow the English one
        using TextFolder folder = new(("en", English), ("de", """{ "Greeting": "Raus hier." }"""));

        Assert.Equal(new[] { "Greeting" }, folder.Read().TextIn("de").Keys);
    }

    [Fact]
    public void TextIn_ThrowsForALanguageWithNoFile()
    {
        using TextFolder folder = TwoLanguages();

        Assert.Throws<ArgumentException>(() => folder.Read().TextIn("nl"));
    }

    [Fact]
    public void ThrowsWhenThereIsNoTextFolder()
    {
        string missing = Path.Combine(Path.GetTempPath(), $"olive-text-{Guid.NewGuid():N}");

        Assert.Throws<DirectoryNotFoundException>(() => new JsonTextProvider(missing));
    }

    [Fact]
    public void ThrowsWhenTheSourceLanguageIsMissing()
    {
        // without it there is nothing for an untranslated language to fall back to, so it fails at
        // startup rather than the first time a player runs the game in an untranslated language
        using TextFolder folder = new(("fr", French));

        Assert.Throws<FileNotFoundException>(() => folder.Read());
    }

    [Fact]
    public void ThrowsWhenALanguageFileIsNotAnObjectOfStrings()
    {
        using TextFolder folder = new(("en", English), ("fr", """{ "Greeting": ["Sors de là."] }"""));

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => folder.Read());

        // the message has to name the file, or a broken translation is a hunt
        Assert.Contains("fr.json", exception.Message);
    }
}

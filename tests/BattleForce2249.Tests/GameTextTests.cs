using System.Globalization;
using OliveGameStudio;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// Covers the game's user-facing text and its translations: that every language says something,
/// says it for every key English has, and reaches the player when their language is set.
/// </summary>
public sealed class GameTextTests
{
    /// <summary>
    /// The shipped language files, read straight from the folder the build copies them into, so a
    /// test can ask for one specific language rather than whichever one the machine running the
    /// tests happens to be in — and so a file that fails to ship shows up here.
    /// </summary>
    static readonly JsonTextProvider _text =
        new(Path.Combine(AppContext.BaseDirectory, GameText.TextFolderName));

    /// <summary>
    /// The English source text. Every translation is measured against this.
    /// </summary>
    const string Quest1TitleInEnglish = "Get out. The debris field is collapsing around you.";

    /// <summary>
    /// The English name of the character the game is played as.
    /// </summary>
    const string DisgracedNameInEnglish = "The Disgraced";

    /// <summary>
    /// Gets every language the game is translated into, English aside.
    /// </summary>
    public static IEnumerable<object[]> TranslatedCultures =>
        new[] { "fr", "it", "de", "es", "pt-BR", "ja" }.Select(culture => new object[] { culture });

    [Fact]
    public void EnglishIsTheSourceLanguage()
    {
        Assert.Equal(JsonTextProvider.DefaultSourceLanguage, _text.SourceLanguage);
        Assert.Equal(Quest1TitleInEnglish, _text.TextIn("en")["Quest1Title"]);
        Assert.Equal(DisgracedNameInEnglish, _text.TextIn("en")["DisgracedName"]);
    }

    [Fact]
    public void EveryLanguageTheGameShipsWithIsThere()
    {
        // the files are copied next to the game rather than compiled in, so a missing one is a
        // packaging mistake this catches
        Assert.Equal(
            new[] { "de", "en", "es", "fr", "it", "ja", "pt-BR" },
            _text.Languages.OrderBy(language => language, StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(TranslatedCultures))]
    public void EveryLanguageTranslatesEveryKeyEnglishHas(string culture)
    {
        // a language that is short a key would silently fall back to English in game
        Assert.Equal(
            _text.TextIn("en").Keys.OrderBy(key => key),
            _text.TextIn(culture).Keys.OrderBy(key => key));
    }

    [Theory]
    [MemberData(nameof(TranslatedCultures))]
    public void NoTranslationIsBlank(string culture)
    {
        Assert.All(
            _text.TextIn(culture).Values,
            translated => Assert.False(string.IsNullOrWhiteSpace(translated)));
    }

    [Theory]
    [MemberData(nameof(TranslatedCultures))]
    public void NoTranslationIsLeftInEnglish(string culture)
    {
        // catches a language file that was copied from English and never translated
        Assert.All(
            _text.TextIn("en"),
            english => Assert.NotEqual(english.Value, _text.TextIn(culture)[english.Key]));
    }

    [Fact]
    public void Quest1Title_IsReadInThePlayersLanguage()
    {
        using CultureScope _ = new("fr");

        Assert.Equal("Sors de là. Le champ de débris s'effondre autour de toi.", GameText.Quest1Title);
    }

    [Fact]
    public void Quest1Title_IsEnglish_WhenThePlayersLanguageIsNotTranslated()
    {
        // Dutch has no translation, so the player reads English rather than a blank or a key
        using CultureScope _ = new("nl");

        Assert.Equal(Quest1TitleInEnglish, GameText.Quest1Title);
    }

    [Fact]
    public void ARegionalLanguage_FallsBackToItsBaseLanguage()
    {
        // fr-CA has no file of its own, so it reads the French one
        using CultureScope _ = new("fr-CA");

        Assert.Equal(_text.Get("Quest1Title", new CultureInfo("fr")), GameText.Quest1Title);
    }

    [Fact]
    public void Get_ThrowsForAKeyThatExistsInNoLanguage()
    {
        Assert.Throws<MissingTextException>(() => GameText.Get("not-a-key"));
    }

    [Fact]
    public void DisgracedName_IsReadInThePlayersLanguage()
    {
        using CultureScope _ = new("de");

        Assert.Equal("Der Geächtete", GameText.DisgracedName);
    }

    [Fact]
    public void DisgracedName_IsEnglish_WhenThePlayersLanguageIsNotTranslated()
    {
        using CultureScope _ = new("nl");

        Assert.Equal(DisgracedNameInEnglish, GameText.DisgracedName);
    }

    [Fact]
    public void TheRoster_TakesTheCharactersNameFromTheTranslations()
    {
        using CultureScope _ = new("ja");

        Assert.Equal("汚名を着せられし者", new BattleForceRoster().Starting.Name);
    }

    [Fact]
    public void TheCampaign_TakesItsQuestTitleFromTheTranslations()
    {
        using CultureScope _ = new("ja");

        BattleForceCampaign campaign = new();
        QuestDefinition quest1 = campaign.Quests.Single(quest => quest.Id == BattleForceCampaign.Quest1Id);

        Assert.Equal("脱出しろ。周囲のデブリ帯が崩壊していく。", quest1.Title);
    }

    [Fact]
    public void TheCampaignTitle_FollowsALanguageChange()
    {
        // the campaign is a singleton, so a title resolved once at startup would leave the player
        // reading whichever language the game happened to launch in
        BattleForceCampaign campaign = new();

        string french;
        using (CultureScope _ = new("fr"))
        {
            french = campaign.Quests.Single().Title;
        }

        using CultureScope __ = new("de");

        Assert.Equal("Sors de là. Le champ de débris s'effondre autour de toi.", french);
        Assert.Equal(
            "Raus hier. Das Trümmerfeld bricht um dich herum zusammen.",
            campaign.Quests.Single().Title);
    }

    [Fact]
    public void TheQuestId_IsNotTranslated()
    {
        // ids go into save games, so a save written in one language must load in another
        using CultureScope _ = new("de");

        BattleForceCampaign campaign = new();

        Assert.Contains(campaign.Quests, quest => quest.Id == "quest-1");
    }
}

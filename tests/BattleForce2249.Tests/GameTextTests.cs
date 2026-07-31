using System.Collections;
using System.Globalization;
using System.Resources;
using Pilgrimage;

namespace BattleForce2249.Tests;

/// <summary>
/// Covers the game's user-facing text and its translations: that every language says something, says
/// it for every key English has, and reaches the player when their language is set.
/// </summary>
public sealed class GameTextTests
{
    /// <summary>
    /// Reads the resx family directly, so a test can ask for one specific language rather than
    /// whichever one the machine running the tests happens to be in.
    /// </summary>
    static readonly ResourceManager _resources = new(typeof(GameText));

    /// <summary>
    /// The English source text. Every translation is measured against this.
    /// </summary>
    const string Quest1TitleInEnglish = "Get out. The debris field is collapsing around you.";

    /// <summary>
    /// Gets every language the game is translated into, English aside.
    /// </summary>
    public static IEnumerable<object[]> TranslatedCultures =>
        new[] { "fr", "it", "de", "es", "pt-BR", "ja" }.Select(culture => new object[] { culture });

    /// <summary>
    /// Reads the keys a single language defines, without falling back to English — so a language
    /// that is missing a key shows up as missing rather than borrowing the English one.
    /// </summary>
    static IReadOnlyCollection<string> KeysFor(CultureInfo culture)
    {
        ResourceSet set =
            _resources.GetResourceSet(culture, createIfNotExists: true, tryParents: false)
            ?? throw new InvalidOperationException($"There is no game text at all for '{culture.Name}'.");

        return set.Cast<DictionaryEntry>().Select(entry => (string)entry.Key).ToList();
    }

    static string StringFor(string culture, string key) =>
        _resources.GetString(key, new CultureInfo(culture))
        ?? throw new InvalidOperationException($"No '{key}' for culture '{culture}'.");

    [Fact]
    public void EnglishIsTheSourceLanguage()
    {
        Assert.Equal(Quest1TitleInEnglish, _resources.GetString("Quest1Title", CultureInfo.InvariantCulture));
    }

    [Theory]
    [MemberData(nameof(TranslatedCultures))]
    public void EveryLanguageTranslatesEveryKeyEnglishHas(string culture)
    {
        // a language that is short a key would silently fall back to English in game
        Assert.Equal(
            KeysFor(CultureInfo.InvariantCulture).OrderBy(key => key),
            KeysFor(new CultureInfo(culture)).OrderBy(key => key));
    }

    [Theory]
    [MemberData(nameof(TranslatedCultures))]
    public void NoTranslationIsBlank(string culture)
    {
        Assert.All(
            KeysFor(new CultureInfo(culture)),
            key => Assert.False(string.IsNullOrWhiteSpace(StringFor(culture, key))));
    }

    [Theory]
    [MemberData(nameof(TranslatedCultures))]
    public void NoTranslationIsLeftInEnglish(string culture)
    {
        // catches a language file that was copied from English and never translated
        Assert.All(
            KeysFor(CultureInfo.InvariantCulture),
            key => Assert.NotEqual(
                _resources.GetString(key, CultureInfo.InvariantCulture),
                StringFor(culture, key)));
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

        Assert.Equal(StringFor("fr", "Quest1Title"), GameText.Quest1Title);
    }

    [Fact]
    public void Get_ThrowsForAKeyThatExistsInNoLanguage()
    {
        Assert.Throws<MissingManifestResourceException>(() => GameText.Get("not-a-key"));
    }

    [Fact]
    public void TheCampaign_TakesItsQuestTitleFromTheTranslations()
    {
        using CultureScope _ = new("ja");

        // the campaign reads its title when it is built, so it has to be built inside the scope
        BattleForceCampaign campaign = new();
        QuestDefinition quest1 = campaign.Quests.Single(quest => quest.Id == BattleForceCampaign.Quest1Id);

        Assert.Equal("脱出しろ。周囲のデブリ帯が崩壊していく。", quest1.Title);
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

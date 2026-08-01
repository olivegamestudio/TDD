using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OliveGameStudio;

namespace BattleForce2249.Tests;

/// <summary>
/// Covers binding options from configuration, including the real appsettings files that ship
/// with the game. A mistyped section name binds silently to nothing and leaves the defaults in
/// place, so these tests assert the configured values actually arrive.
/// </summary>
public sealed class ConfigurationBindingTests
{
    static IConfiguration InMemory(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    static IConfiguration ShippedConfiguration(params string[] fileNames)
    {
        ConfigurationBuilder builder = new();
        builder.SetBasePath(AppContext.BaseDirectory);

        foreach (string fileName in fileNames)
        {
            builder.AddJsonFile(fileName, optional: false);
        }

        return builder.Build();
    }

    static ServiceProvider BuildProvider(
        IConfiguration configuration,
        Action<CompanyScreenOptions>? configure = null)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddBattleForce(configuration, configure);

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }

    [Fact]
    public void BindsTheCompanyScreenSection()
    {
        using ServiceProvider provider = BuildProvider(InMemory(
            ("CompanyScreen:Duration", "00:00:05")));

        Assert.Equal(
            TimeSpan.FromSeconds(5),
            provider.GetRequiredService<IOptions<CompanyScreenOptions>>().Value.Duration);
    }

    [Fact]
    public void KeepsDefaults_WhenTheConfigurationIsEmpty()
    {
        using ServiceProvider provider = BuildProvider(InMemory());

        Assert.Equal(
            TimeSpan.FromSeconds(2),
            provider.GetRequiredService<IOptions<CompanyScreenOptions>>().Value.Duration);
    }

    [Fact]
    public void CodeConfiguration_OverridesTheBoundValue()
    {
        // layering order: file first, then code
        using ServiceProvider provider = BuildProvider(
            InMemory(("CompanyScreen:Duration", "00:00:05")),
            options => options.Duration = TimeSpan.FromSeconds(9));

        Assert.Equal(
            TimeSpan.FromSeconds(9),
            provider.GetRequiredService<IOptions<CompanyScreenOptions>>().Value.Duration);
    }

    [Fact]
    public void InvalidConfiguredValue_IsStillValidated()
    {
        using ServiceProvider provider = BuildProvider(InMemory(
            ("CompanyScreen:Duration", "-00:00:01")));

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<CompanyScreenOptions>>().Value);
    }

    [Fact]
    public void ShippedAppSettings_HasTheSectionsTheCodeBindsFrom()
    {
        // Asserted on the sections rather than the bound values, because the shipped release
        // values equal the code defaults: a mistyped section name would fall back to those
        // defaults and a value assertion alone would still pass.
        IConfiguration configuration = ShippedConfiguration("appsettings.json");

        Assert.True(
            configuration.GetSection(CompanyScreenOptions.SectionName).Exists(),
            $"appsettings.json is missing the '{CompanyScreenOptions.SectionName}' section");
        Assert.NotNull(configuration[$"{CompanyScreenOptions.SectionName}:Duration"]);

        Assert.True(
            configuration.GetSection(DisplayOptions.SectionName).Exists(),
            $"appsettings.json is missing the '{DisplayOptions.SectionName}' section");
        Assert.NotNull(
            configuration[$"{DisplayOptions.SectionName}:{nameof(DisplayOptions.WidestSupportedViewportInPixels)}"]);
    }

    [Fact]
    public void BindsTheDisplaySection()
    {
        using ServiceProvider provider = BuildProvider(InMemory(
            ($"Display:{nameof(DisplayOptions.WidestSupportedViewportInPixels)}", "5120")));

        Assert.Equal(
            5_120f,
            provider.GetRequiredService<IOptions<DisplayOptions>>().Value.WidestSupportedViewportInPixels);
    }

    [Fact]
    public void ConfiguredDisplay_ReachesTheStarField()
    {
        // the value has to arrive where the floor is derived, not merely bind: the whole point of
        // #50 is that one declaration decides what content is held against
        using ServiceProvider provider = BuildProvider(InMemory(
            ($"Display:{nameof(DisplayOptions.WidestSupportedViewportInPixels)}", "5120")));

        Assert.Equal(5_120f, provider.GetRequiredService<StarField>().WidestSupportedViewportInPixels);
    }

    [Fact]
    public void ShippedAppSettings_DeclaresTheDisplayTheCodeDefaultsTo()
    {
        // the file and the code default say the same thing; a build that wants a different screen
        // changes the file, and nothing in the game restates the number
        using ServiceProvider provider = BuildProvider(ShippedConfiguration("appsettings.json"));

        Assert.Equal(
            DisplayOptions.DefaultWidestSupportedViewportInPixels,
            provider.GetRequiredService<IOptions<DisplayOptions>>().Value.WidestSupportedViewportInPixels);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1920")]
    public void InvalidConfiguredDisplay_IsRefused(string width)
    {
        // a display of nothing is not a narrower screen, it is one no content can be held against
        using ServiceProvider provider = BuildProvider(InMemory(
            ($"Display:{nameof(DisplayOptions.WidestSupportedViewportInPixels)}", width)));

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<DisplayOptions>>().Value);
    }

    [Fact]
    public void ShippedAppSettings_BindsToTheReleaseValues()
    {
        // the actual file that ships, loaded the same way Program.cs loads it
        using ServiceProvider provider = BuildProvider(ShippedConfiguration("appsettings.json"));

        Assert.Equal(
            TimeSpan.FromSeconds(2),
            provider.GetRequiredService<IOptions<CompanyScreenOptions>>().Value.Duration);
    }

    [Fact]
    public void ShippedDevelopmentOverlay_ShortensTheSplash()
    {
        using ServiceProvider provider = BuildProvider(
            ShippedConfiguration("appsettings.json", "appsettings.Development.json"));

        Assert.Equal(
            TimeSpan.FromMilliseconds(250),
            provider.GetRequiredService<IOptions<CompanyScreenOptions>>().Value.Duration);
    }

    [Fact]
    public void DevelopmentSplash_IsShortenedButNotSkipped()
    {
        // a zero duration would stop debug builds exercising the splash transition at all
        IConfiguration configuration = ShippedConfiguration("appsettings.Development.json");

        TimeSpan duration = configuration
            .GetSection(CompanyScreenOptions.SectionName)
            .Get<CompanyScreenOptions>()!
            .Duration;

        Assert.True(duration > TimeSpan.Zero, "the development splash must still run");
        Assert.True(duration < TimeSpan.FromSeconds(2), "the development splash must be shorter than release");
    }
}

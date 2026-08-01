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
        Action<CompanyScreenOptions>? configure = null,
        Action<DisplayOptions>? configureDisplay = null)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddBattleForce(configuration, configure, configureDisplay);

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
            configuration[
                $"{DisplayOptions.SectionName}:{nameof(DisplayOptions.WidestSupportedViewportInPixels)}"]);
    }

    [Fact]
    public void BindsTheDisplaySection()
    {
        using ServiceProvider provider = BuildProvider(InMemory(
            ($"{DisplayOptions.SectionName}:{nameof(DisplayOptions.WidestSupportedViewportInPixels)}", "5120")));

        Assert.Equal(
            5120f,
            provider.GetRequiredService<IOptions<DisplayOptions>>().Value.WidestSupportedViewportInPixels);
    }

    [Fact]
    public void KeepsTheDeclaredDisplay_WhenTheConfigurationIsEmpty()
    {
        // the game states a supported screen whether or not anyone configures one — that is the
        // point of stating it, and #50 is what the absence of the statement cost
        using ServiceProvider provider = BuildProvider(InMemory());

        Assert.Equal(
            7680f,
            provider.GetRequiredService<IOptions<DisplayOptions>>().Value.WidestSupportedViewportInPixels);
    }

    [Fact]
    public void CodeConfiguration_OverridesTheDeclaredDisplay()
    {
        using ServiceProvider provider = BuildProvider(
            InMemory(($"{DisplayOptions.SectionName}:{nameof(DisplayOptions.WidestSupportedViewportInPixels)}", "5120")),
            configureDisplay: options => options.WidestSupportedViewportInPixels = 3840f);

        Assert.Equal(
            3840f,
            provider.GetRequiredService<IOptions<DisplayOptions>>().Value.WidestSupportedViewportInPixels);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1920")]
    [InlineData("NaN")]
    public void InvalidDisplay_IsRefusedWhereItIsConfigured(string widest)
    {
        // not a display anything can be measured against, and a floor derived from it would accept
        // every layer put to it. Failing at the section that carries it says which line to change.
        using ServiceProvider provider = BuildProvider(InMemory(
            ($"{DisplayOptions.SectionName}:{nameof(DisplayOptions.WidestSupportedViewportInPixels)}", widest)));

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<DisplayOptions>>().Value);
    }

    [Fact]
    public void TheStarFieldIsFlooredAgainstTheConfiguredDisplay()
    {
        // the wiring end to end: a build that declares a different screen gets a star field that
        // holds its layers against that screen, without anything else being told about it
        using ServiceProvider provider = BuildProvider(InMemory(
            ($"{DisplayOptions.SectionName}:{nameof(DisplayOptions.WidestSupportedViewportInPixels)}", "5120")));

        Assert.Equal(5120f, provider.GetRequiredService<StarField>().WidestSupportedViewportInPixels);
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

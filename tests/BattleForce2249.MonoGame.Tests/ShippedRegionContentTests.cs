using OliveGameStudio;

namespace BattleForce2249.MonoGame.Tests;

/// <summary>
/// Holds every sprite the shipped regions name to actually being in the content build.
/// </summary>
/// <remarks>
/// <para>
/// A texture that is not built is not a gap on screen — it is an exception the first time the
/// region is drawn, which is when the player enters the world. This has already happened once:
/// <c>glow.png</c> sat in the content folder, was used by seventeen bodies in the debris field, and
/// was never listed in <c>Content.mgcb</c>, so the game crashed on entering the game screen while
/// every test stayed green.
/// </para>
/// <para>
/// <b>The two lists are different things, which is what made it possible.</b> The importer checked
/// the content <em>folder</em> — where the file was — but only what is registered in
/// <c>Content.mgcb</c> is actually built. Nothing compared the regions against the build until
/// this did.
/// </para>
/// <para>
/// It reads the repository rather than the build output on purpose: the point is to fail when a
/// region names something the content build does not carry, and reading the built output could
/// only tell us what was built.
/// </para>
/// </remarks>
public sealed class ShippedRegionContentTests
{
    /// <summary>
    /// The repository root, found by walking up from the test binary until the solution appears.
    /// </summary>
    static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OliveGameStudio.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }

    /// <summary>
    /// Every asset key the content build produces, taken from the <c>/build:</c> lines in
    /// <c>Content.mgcb</c> — which is what the build actually acts on.
    /// </summary>
    static HashSet<string> BuiltAssetKeys()
    {
        string mgcb = Path.Combine(RepositoryRoot(), "BattleForce2249.MonoGame", "Content", "Content.mgcb");

        return
        [
            .. File.ReadAllLines(mgcb)
                .Where(line => line.StartsWith("/build:", StringComparison.Ordinal))
                .Select(line => Path.GetFileNameWithoutExtension(line["/build:".Length..].Trim()))
        ];
    }

    static IEnumerable<(string Region, SceneDefinition Scene)> ShippedRegions()
    {
        string folder = Path.Combine(RepositoryRoot(), "src", "BattleForce2249.Game", "Regions");

        foreach (string file in Directory.EnumerateFiles(folder, "*.json"))
        {
            yield return (Path.GetFileNameWithoutExtension(file), SceneSerializer.Deserialize(File.ReadAllText(file)));
        }
    }

    [Fact]
    public void EverySpriteARegionNames_IsInTheContentBuild()
    {
        HashSet<string> built = BuiltAssetKeys();

        List<string> missing =
        [
            .. from region in ShippedRegions()
               from sprite in region.Scene.Bodies.Select(body => body.Sprite).Distinct()
               where !built.Contains(sprite)
               select $"{region.Region} names '{sprite}'"
        ];

        Assert.Empty(missing);
    }

    [Fact]
    public void TheRegionsFolder_IsNotEmpty()
    {
        // Guards the test above from passing because it found nothing to check.
        Assert.NotEmpty(ShippedRegions());
    }

    [Fact]
    public void EveryShippedRegion_CanBeRead()
    {
        // Deserialising is deliberately forgiving and hands back an empty region rather than
        // failing, which is right for the player and silent for everybody else.
        Assert.All(ShippedRegions(), region => Assert.NotEmpty(region.Scene.Bodies));
    }
}

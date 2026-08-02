namespace Pilgrimage.Tests;

/// <summary>
/// QA's adversarial cover for the rule #44 decided at the quest log's edge: an entry that
/// <em>names no quest</em> is drift and is skipped, while an entry that <em>is not there</em> is
/// refused and takes the whole batch with it.
/// </summary>
/// <remarks>
/// <para>
/// The cover already on the branch pins one identifier shape per case and puts the <c>null</c>
/// entry last. These attack the same rule from where it is easier to get wrong: every shape of
/// blank the framework calls whitespace rather than the three that were written out, the refusal
/// from every position in the batch rather than the end of it, and unnamed entries scattered
/// through duplicates so the skip has to compose with the furthest-state rule #72 left in the same
/// loop.
/// </para>
/// <para>
/// The property being defended is that an unnamed entry costs exactly itself. It must not take the
/// progress standing beside it — that was the reported defect — and it must not quietly consume
/// another quest's first-entry slot either, which would silently change what a duplicate resolves
/// to.
/// </para>
/// </remarks>
public sealed class QuestLogUnnamedEntryAdversarialTests
{
    static QuestDefinition Definition(string id) =>
        new(id,
            $"Title of {id}",
            new QuestTrigger(QuestTriggerKind.Proximity, 25),
            new QuestTrigger(QuestTriggerKind.Proximity, 50),
            AutoStarts: true);

    static QuestLog LogWith(params string[] ids)
    {
        QuestLog log = new();
        foreach (string id in ids)
        {
            log.Register(Definition(id));
        }

        return log;
    }

    /// <summary>
    /// Every shape the framework treats as blank, not only the empty string and a run of spaces.
    /// The rule is stated in terms of "names no quest", so it has to hold for a tab, a line break
    /// and the space characters a hand-edited file can pick up from a paste as well.
    /// </summary>
    public static TheoryData<string, string?> IdentifiersNamingNothing =>
        new()
        {
            { "null", null },
            { "empty", "" },
            { "spaces", "   " },
            { "tab", "\t" },
            { "line feed", "\n" },
            { "carriage return and line feed", "\r\n" },
            { "no-break space", " " },
            { "em space", " " },
        };

    [Theory]
    [MemberData(nameof(IdentifiersNamingNothing))]
    public void Restore_SkipsEveryShapeOfIdentifierThatNamesNothing(string because, string? questId)
    {
        Assert.NotNull(because);
        QuestLog log = LogWith("quest-1");

        log.Restore([
            new QuestProgress(questId!, QuestState.Active),
            new QuestProgress("quest-1", QuestState.Completed),
        ]);

        Assert.Equal(QuestState.Completed, log.Find("quest-1")!.State);
    }

    [Theory]
    [MemberData(nameof(IdentifiersNamingNothing))]
    public void Restore_LeavesTheLogAsItWas_WhenEveryEntryNamesNothing(string because, string? questId)
    {
        Assert.NotNull(because);
        QuestLog log = LogWith("quest-1", "quest-2");

        log.Restore([
            new QuestProgress(questId!, QuestState.Completed),
            new QuestProgress(questId!, QuestState.Active),
        ]);

        // A file that says nothing about any registered quest restores nothing, rather than
        // restoring something arbitrary because two entries happened to share an identifier.
        Assert.All(log.Quests, quest => Assert.Equal(QuestState.NotStarted, quest.State));
    }

    /// <summary>
    /// An unnamed entry must not enter the set that decides which entry counts as a quest's first.
    /// If it did, a quest named after it would be resolved as a duplicate and could be held back by
    /// the furthest-state rule — the entry naming nothing would be deciding something.
    /// </summary>
    [Fact]
    public void Restore_DoesNotLetAnUnnamedEntryConsumeAQuestsFirstEntry()
    {
        QuestLog log = LogWith("quest-1");

        log.Restore([
            new QuestProgress("", QuestState.Completed),
            new QuestProgress("  ", QuestState.Completed),
            new QuestProgress("quest-1", QuestState.Active),
        ]);

        // Active is behind Completed. Had either blank entry been taken as quest-1's first, the
        // furthest-state rule would have dropped this one and left the quest NotStarted.
        Assert.Equal(QuestState.Active, log.Find("quest-1")!.State);
    }

    /// <summary>
    /// The skip has to compose with #72's furthest-state rule, which lives in the same loop. The
    /// expected value comes from an oracle that knows only the rule — the furthest state any entry
    /// naming the quest gives it — so an implementation that resolved duplicates by position, or
    /// that let a skipped entry shift what "first" means, fails here rather than passing by luck.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void Restore_KeepsTheFurthestState_WhenUnnamedEntriesAreScatteredThroughDuplicates(int seed)
    {
        Random random = new(seed);
        QuestLog log = LogWith("quest-1", "quest-2", "quest-3");

        QuestState[] states = [QuestState.NotStarted, QuestState.Active, QuestState.Completed];
        string?[] blanks = [null, "", "   ", "\t", " "];

        List<QuestProgress> entries = [];
        for (int i = 0; i < 40; i++)
        {
            entries.Add(random.Next(3) == 0
                ? new QuestProgress(blanks[random.Next(blanks.Length)]!, states[random.Next(states.Length)])
                : new QuestProgress($"quest-{random.Next(1, 4)}", states[random.Next(states.Length)]));
        }

        log.Restore(entries);

        foreach (Quest quest in log.Quests)
        {
            QuestState[] named = [.. entries
                .Where(entry => entry.QuestId == quest.Id)
                .Select(entry => entry.State)];

            QuestState expected = named.Length == 0
                ? QuestState.NotStarted
                : named.Max();

            Assert.Equal(expected, quest.State);
        }
    }

    /// <summary>
    /// The refusal is all-or-nothing wherever the entry sits, not only when it is last. A check
    /// that ran inside the applying loop would pass the existing cover — the entry it refuses on is
    /// after the one it applies — and leave the log half restored for every earlier position.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Restore_AppliesNothing_WhereverTheEntryThatIsNotThereSits(int index)
    {
        QuestLog log = LogWith("quest-1", "quest-2");

        List<QuestProgress> entries = [
            new QuestProgress("quest-1", QuestState.Completed),
            new QuestProgress("", QuestState.Active),
            new QuestProgress("quest-2", QuestState.Completed),
        ];
        entries.Insert(index, null!);

        Assert.Throws<ArgumentException>("progress", () => log.Restore(entries));

        Assert.All(log.Quests, quest => Assert.Equal(QuestState.NotStarted, quest.State));
    }

    /// <summary>
    /// An entry that is not there outranks one that names nothing: a batch holding both is refused
    /// rather than reduced to the entries that could be skipped.
    /// </summary>
    [Fact]
    public void Restore_RefusesTheBatch_WhenEveryOtherEntryNamesNothing()
    {
        QuestLog log = LogWith("quest-1");

        Assert.Throws<ArgumentException>("progress", () => log.Restore([
            new QuestProgress("", QuestState.Active),
            null!,
            new QuestProgress("   ", QuestState.Completed),
        ]));

        Assert.Equal(QuestState.NotStarted, log.Find("quest-1")!.State);
    }

    /// <summary>
    /// Refusing the batch before applying any of it means reading the sequence twice, so a caller
    /// handing over something that can only be walked once must still be read correctly. This is
    /// the cost of the all-or-nothing guarantee, and it is the part a later change would break
    /// without noticing — every other test passes a list.
    /// </summary>
    [Fact]
    public void Restore_ReadsASequenceThatCanOnlyBeWalkedOnce()
    {
        QuestLog log = LogWith("quest-1");

        log.Restore(WalkableOnce());

        Assert.Equal(QuestState.Completed, log.Find("quest-1")!.State);
    }

    static IEnumerable<QuestProgress> WalkableOnce()
    {
        bool walked = false;

        IEnumerable<QuestProgress> Entries()
        {
            if (walked)
            {
                throw new InvalidOperationException("The saved progress was walked twice.");
            }

            walked = true;
            yield return new QuestProgress("", QuestState.Active);
            yield return new QuestProgress("quest-1", QuestState.Completed);
        }

        return Entries();
    }
}

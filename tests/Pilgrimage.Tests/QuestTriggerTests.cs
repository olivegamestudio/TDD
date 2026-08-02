namespace Pilgrimage.Tests;

public sealed class QuestTriggerTests
{
    [Fact]
    public void DescribesWhatFiresItAndHowClose()
    {
        QuestTrigger trigger = new(QuestTriggerKind.Proximity, 50);

        Assert.Equal(QuestTriggerKind.Proximity, trigger.Kind);
        Assert.Equal(50, trigger.Distance);
    }

    [Fact]
    public void RejectsANegativeDistance()
    {
        // no position could ever satisfy it, so it would silently never fire
        Assert.Throws<ArgumentOutOfRangeException>(() => new QuestTrigger(QuestTriggerKind.Proximity, -1));
    }

    [Fact]
    public void AllowsAZeroDistance()
    {
        QuestTrigger trigger = new(QuestTriggerKind.Proximity, 0);

        Assert.Equal(0, trigger.Distance);
    }

    [Fact]
    public void EqualityIsByValue()
    {
        Assert.Equal(
            new QuestTrigger(QuestTriggerKind.Proximity, 25),
            new QuestTrigger(QuestTriggerKind.Proximity, 25));

        Assert.NotEqual(
            new QuestTrigger(QuestTriggerKind.Proximity, 25),
            new QuestTrigger(QuestTriggerKind.Proximity, 50));
    }
}

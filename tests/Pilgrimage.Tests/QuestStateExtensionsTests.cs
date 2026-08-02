namespace Pilgrimage.Tests;

/// <summary>
/// Covers the one place the quest lifecycle is treated as an order rather than a set of values.
/// </summary>
public sealed class QuestStateExtensionsTests
{
    [Theory]
    [InlineData(QuestState.NotStarted, QuestState.Active)]
    [InlineData(QuestState.NotStarted, QuestState.Completed)]
    [InlineData(QuestState.Active, QuestState.Completed)]
    public void IsBehind_IsTrue_ForAStateEarlierInTheLifecycle(QuestState state, QuestState other)
    {
        Assert.True(state.IsBehind(other));
    }

    [Theory]
    [InlineData(QuestState.Active, QuestState.NotStarted)]
    [InlineData(QuestState.Completed, QuestState.NotStarted)]
    [InlineData(QuestState.Completed, QuestState.Active)]
    public void IsBehind_IsFalse_ForAStateLaterInTheLifecycle(QuestState state, QuestState other)
    {
        Assert.False(state.IsBehind(other));
    }

    [Theory]
    [InlineData(QuestState.NotStarted)]
    [InlineData(QuestState.Active)]
    [InlineData(QuestState.Completed)]
    public void IsBehind_IsFalse_ForTheSameState(QuestState state)
    {
        // strictly behind, so a state does not trail itself — two entries saying the same thing
        // disagree about nothing
        Assert.False(state.IsBehind(state));
    }

    [Fact]
    public void IsBehind_IsFalse_ForAStateThatIsNotAState_InEitherPosition()
    {
        // A value outside the lifecycle is not early progress, it is not progress. Saying it
        // trails something would quietly swallow it here, and refusing it belongs to
        // Quest.Restore, which is the one place that can say why.
        QuestState nonsense = (QuestState)99;

        Assert.False(nonsense.IsBehind(QuestState.Completed));
        Assert.False(QuestState.NotStarted.IsBehind(nonsense));
    }

    [Fact]
    public void IsBehind_ReadsTheLifecycleOrder_NotTheDeclarationOrderByLuck()
    {
        // the ordering is load bearing, so it is asserted rather than left to whoever next edits
        // the enum to notice
        Assert.True(QuestState.NotStarted.IsBehind(QuestState.Active));
        Assert.True(QuestState.Active.IsBehind(QuestState.Completed));
        Assert.True(QuestState.NotStarted.IsBehind(QuestState.Completed));
    }
}

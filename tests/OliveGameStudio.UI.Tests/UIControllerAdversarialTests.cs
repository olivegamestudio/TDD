namespace OliveGameStudio.Tests;

/// <summary>
/// QA's adversarial pass over #13 — the cases that try to break identity semantics rather than
/// confirm them. Each one is a way a name-matching controller would still be reachable: through
/// the focus path rather than the enabled path, through a name that is null or empty rather than
/// a word, through a disabled twin, or through the moment <see cref="UIController.Add"/> rejects
/// a duplicate.
/// </summary>
public sealed class UIControllerAdversarialTests
{
    // ---- the focus path, which the enabled-state tests do not reach ---------

    [Fact]
    public void DisablingASameNamedSibling_DoesNotStealFocusFromTheFocusedButton()
    {
        // SetEnabled re-homes focus when the button being disabled is the focused one, and it
        // decides that with ==. Under value equality the sibling *was* the focused button, so
        // disabling one screen's BACK unfocused another screen's BACK as well.
        UIController controller = new();
        Button menuBack = new("BACK");
        Button optionsBack = new("BACK");
        controller.Add(menuBack);
        controller.Add(optionsBack);

        controller.Disable(optionsBack);

        Assert.Same(menuBack, controller.Focused);
        Assert.True(controller.IsEnabled(menuBack));
        Assert.False(controller.IsEnabled(optionsBack));
    }

    [Fact]
    public void DisablingTheFocusedButton_ReHomesToItsOwnNeighbour_NotTheFirstMatchingName()
    {
        // the re-home scans for the first *enabled* node. With every node sharing a name, a
        // name-matching controller cannot tell which one it just disabled.
        UIController controller = new();
        Button first = new("BACK");
        Button second = new("BACK");
        Button third = new("BACK");
        controller.Add(first);
        controller.Add(second);
        controller.Add(third);

        controller.Disable(first);

        Assert.Same(second, controller.Focused);
        Assert.False(controller.IsEnabled(first));
        Assert.True(controller.IsEnabled(second));
        Assert.True(controller.IsEnabled(third));
    }

    [Fact]
    public void PressingADisabledButton_DoesNotFireItsEnabledSameNamedSibling()
    {
        // the sharpest version of the defect: Press resolves the button, finds its twin's
        // *enabled* node, focuses the disabled one and fires the twin's action anyway.
        UIController controller = new();
        Button menuBack = new("BACK");
        Button optionsBack = new("BACK");
        controller.Add(menuBack);
        controller.Add(optionsBack);

        List<string> fired = [];
        controller.OnPressed(menuBack, () => fired.Add("menu"));
        controller.OnPressed(optionsBack, () => fired.Add("options"));
        controller.Disable(optionsBack);

        controller.Press(optionsBack);

        Assert.Empty(fired);
        Assert.Same(menuBack, controller.Focused);
        Assert.Null(controller.Held);
    }

    [Fact]
    public void DisablingAHeldButton_MidPress_CommitsNothing_AndSpareItsSibling()
    {
        UIController controller = new();
        Button menuBack = new("BACK");
        Button optionsBack = new("BACK");
        controller.Add(menuBack);
        controller.Add(optionsBack);

        List<string> committed = [];
        controller.OnReleased(menuBack, () => committed.Add("menu"));
        controller.OnReleased(optionsBack, () => committed.Add("options"));

        controller.Press(optionsBack);
        controller.Disable(optionsBack);
        controller.Release();

        Assert.Empty(committed);
        Assert.True(controller.IsEnabled(menuBack));
    }

    // ---- names that are not words ------------------------------------------

    [Fact]
    public void ButtonsWithNoNameAtAll_AreStillTwoButtons()
    {
        // null == null under value equality, so a pair of unnamed buttons collided the hardest
        // of all. The interpolated exception message must survive a null name too.
        UIController controller = new();
        Button first = new(null!);
        Button second = new(null!);
        controller.Add(first);
        controller.Add(second);

        controller.Disable(second);

        Assert.True(controller.IsEnabled(first));
        Assert.False(controller.IsEnabled(second));
        Assert.Throws<InvalidOperationException>(() => controller.Add(first));
        Assert.Throws<InvalidOperationException>(() => controller.IsEnabled(new Button(null!)));
    }

    [Fact]
    public void ButtonsNamedWithTheEmptyString_AreStillTwoButtons()
    {
        UIController controller = new();
        Button first = new("");
        Button second = new("");
        controller.Add(first);
        controller.Add(second);

        controller.Disable(first);

        Assert.False(controller.IsEnabled(first));
        Assert.True(controller.IsEnabled(second));
        Assert.Same(second, controller.Focused);
    }

    // ---- the moment Add rejects a duplicate ---------------------------------

    [Fact]
    public void ARejectedDuplicateAdd_ChangesNothing_NotEvenFocus()
    {
        // Add adopts focus when nothing is focused. If the duplicate check ran after the
        // element list was touched — or not at all — the rejected add would still have
        // claimed focus. Nothing observable may move.
        UIController controller = new();
        Button start = new("START");
        controller.Add(start);
        controller.UnFocus();

        Assert.Throws<InvalidOperationException>(() => controller.Add(start));

        Assert.Null(controller.Focused);
    }

    [Fact]
    public void ARejectedDuplicateAdd_LeavesTheButtonFullyWorking()
    {
        UIController controller = new();
        Button start = new("START");
        Button quit = new("QUIT");
        controller.Add(start);
        controller.Add(quit);

        List<string> committed = [];
        controller.OnReleased(start, () => committed.Add("start"));

        Assert.Throws<InvalidOperationException>(() => controller.Add(start));

        Assert.Same(start, controller.Focused);
        Assert.True(controller.IsEnabled(start));

        controller.Press(start);
        controller.Release();

        Assert.Equal("start", Assert.Single(committed));
    }

    [Fact]
    public void ADisabledButton_IsStillManaged_AndCannotBeAddedAgain()
    {
        // "already managed" has to mean held, not enabled — re-adding a disabled button would
        // otherwise slip a second, unreachable node past the guard.
        UIController controller = new();
        Button start = new("START");
        Button quit = new("QUIT");
        controller.Add(start);
        controller.Add(quit);
        controller.Disable(start);

        Assert.Throws<InvalidOperationException>(() => controller.Add(start));
    }

    [Fact]
    public void ASameNamedSibling_IsNotADuplicate_AndIsAccepted()
    {
        // the guard must reject the same button, never merely the same name — otherwise it
        // reintroduces the defect at the point of entry.
        UIController controller = new();
        controller.Add(new Button("BACK"));

        controller.Add(new Button("BACK"));
        controller.Add(new Button("BACK"));
    }

    // ---- wiring between siblings -------------------------------------------

    [Fact]
    public void SameNamedSiblings_CanBeLinkedToEachOther()
    {
        // both ends of Link resolve independently; a stranger sharing the name must still fail
        UIController controller = new();
        Button menuBack = new("BACK");
        Button optionsBack = new("BACK");
        controller.Add(menuBack);
        controller.Add(optionsBack);

        controller.Link(menuBack, Direction.Down, optionsBack);
        controller.Link(optionsBack, Direction.Up, menuBack);

        Assert.Throws<InvalidOperationException>(
            () => controller.Link(menuBack, Direction.Left, new Button("BACK")));
    }

    // ---- the type contract, at collection boundaries ------------------------

    [Fact]
    public void ACrowdOfIdenticallyNamedButtons_IsACrowd_NotOne()
    {
        // scale is where a name-keyed set collapses: fifty BACK buttons must stay fifty
        UIController controller = new();
        List<Button> buttons = [.. Enumerable.Range(0, 50).Select(_ => new Button("BACK"))];
        foreach (Button button in buttons)
        {
            controller.Add(button);
        }

        Assert.Equal(50, new HashSet<Button>(buttons).Count);

        controller.Disable(buttons[17]);

        Assert.False(controller.IsEnabled(buttons[17]));
        Assert.All(buttons.Where((_, i) => i != 17), b => Assert.True(controller.IsEnabled(b)));
        Assert.Same(buttons[0], controller.Focused);
    }

    [Fact]
    public void ImagesAndTextAreDistinctKeys_NotJustUnequal()
    {
        // GetHashCode has to agree with equality for the rest of the hierarchy too, or a
        // future asset cache keyed by element silently serves one image for two.
        Image first = new("BACKGROUND");
        Image second = new("BACKGROUND");
        Text left = new("Continue");
        Text right = new("Continue");

        Dictionary<Element, string> owners = new()
        {
            [first] = "menu",
            [second] = "options",
            [left] = "menu",
            [right] = "options",
        };

        Assert.Equal(4, owners.Count);
        Assert.Equal("menu", owners[first]);
        Assert.Equal("options", owners[second]);
        Assert.Equal("menu", owners[left]);
        Assert.Equal("options", owners[right]);
    }

    [Fact]
    public void AButtonIsNotEqualToAnImageOfTheSameName()
    {
        // the hierarchy shares a shape; nothing may make one element type equal another
        Assert.NotEqual<Element>(new Button("BACK"), new Image("BACK"));
        Assert.NotEqual<Element>(new Text("BACK"), new Image("BACK"));
    }

    [Fact]
    public void ElementRemainsDerivable_AndANonButtonNeverTakesFocus()
    {
        // Element became a class so the hierarchy could stop comparing values. A game is still
        // expected to be able to derive from it, and a non-button must stay out of the nodes.
        UIController controller = new();
        Decoration decoration = new();

        controller.Add(decoration);

        Assert.Null(controller.Focused);

        Button start = new("START");
        controller.Add(start);

        Assert.Same(start, controller.Focused);
    }

    sealed class Decoration : Element;
}

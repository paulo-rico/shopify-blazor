using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Polaris.Blazor.DataGrid;
using Polaris.Blazor.Events;

namespace Polaris.Blazor.Components;

/// <summary>
/// Page navigation for a <see cref="PolarisDataGrid{TItem}"/>, sharing its state.
/// </summary>
/// <remarks>
/// <para>
/// Composed rather than wrapped: Polaris has no pagination element. <c>s-table</c> can draw its own
/// previous/next controls, but it cannot say which record of how many the merchant is looking at,
/// cannot offer a page size, and cannot show more than one step in either direction — so the pager is
/// built out of <c>s-button</c> and <c>s-select</c>, and the table's own pagination stays off.
/// </para>
/// <para>
/// Placed separately from the grid on purpose. A list screen usually wants the pager at the bottom
/// of the table, and sometimes in a page's action bar; the component is the same either way because
/// everything it shows comes from the shared state.
/// </para>
/// <para>
/// Nothing here counts rows itself. Every number on screen — the range, the page count, which buttons
/// are enabled — is read from the state after its query ran, which is what keeps the pager and the
/// table from disagreeing about how many pages there are.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;PolarisDataPager State="@State" PageSizeOptions="@(new[] { 10, 25, 50 })" /&gt;
/// </code>
/// </example>
public sealed class PolarisDataPager : PolarisGridComponentBase
{
    /// <summary>The shared state. Required, and the same instance the grid was given.</summary>
    [Parameter, EditorRequired]
    public PolarisGridState State { get; set; } = default!;

    /// <summary>The page sizes offered. The state's own size is added if it is missing.</summary>
    [Parameter] public IReadOnlyList<int> PageSizeOptions { get; set; } = [10, 25, 50, 100];

    /// <summary>Shows first-page and last-page buttons alongside previous and next.</summary>
    [Parameter] public bool ShowFirstLast { get; set; } = true;

    /// <summary>Shows the numbered page buttons.</summary>
    [Parameter] public bool ShowPageNumbers { get; set; } = true;

    /// <summary>Shows the record range, such as "26–50 of 148".</summary>
    [Parameter] public bool ShowRecordRange { get; set; } = true;

    /// <summary>Shows the page-size select.</summary>
    [Parameter] public bool ShowPageSize { get; set; } = true;

    /// <summary>
    /// How many numbered buttons are shown at most, current page included. Values below three are
    /// raised to three: two numbers and an ellipsis describe nothing.
    /// </summary>
    [Parameter] public int MaxPageNumbers { get; set; } = 5;

    /// <inheritdoc />
    protected override PolarisGridState? BoundState => State;

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        // A navigation landmark, so a screen reader can jump to the pager and knows the buttons move
        // within one list rather than submitting anything.
        builder.OpenElement(0, "nav");
        builder.AddAttribute(1, "aria-label", "Pagination");

        builder.OpenComponent<PolarisStack>(2);
        builder.AddAttribute(3, nameof(PolarisStack.Direction), PolarisStackDirection.Inline);
        builder.AddAttribute(4, nameof(PolarisStack.Gap), PolarisGap.Base);
        builder.AddAttribute(5, nameof(PolarisStack.JustifyContent), "space-between");
        builder.AddAttribute(6, nameof(PolarisStack.AlignItems), "center");
        builder.AddAttribute(7, nameof(PolarisStack.ChildContent), (RenderFragment)BuildContent);
        builder.CloseComponent();

        builder.CloseElement();
    }

    private void BuildContent(RenderTreeBuilder builder)
    {
        if (State is null)
        {
            return;
        }

        BuildSummary(builder);
        BuildControls(builder);
    }

    private void BuildSummary(RenderTreeBuilder builder)
    {
        builder.OpenComponent<PolarisStack>(0);
        builder.AddAttribute(1, nameof(PolarisStack.Direction), PolarisStackDirection.Inline);
        builder.AddAttribute(2, nameof(PolarisStack.Gap), PolarisGap.Small);
        builder.AddAttribute(3, nameof(PolarisStack.AlignItems), "center");
        builder.AddAttribute(4, nameof(PolarisStack.ChildContent), (RenderFragment)(content =>
        {
            if (ShowRecordRange)
            {
                content.OpenComponent<PolarisText>(0);
                content.AddAttribute(1, nameof(PolarisText.Color), PolarisColor.Subdued);
                content.AddAttribute(2, nameof(PolarisText.ChildContent), (RenderFragment)(text => text.AddContent(0, RangeText())));
                content.CloseComponent();
            }

            // The page counter is the substitute for the numbered buttons, not an addition to them:
            // with both, "2 of 6" sits next to a "2" that already says it.
            if (!ShowPageNumbers && State.PageCount > 0)
            {
                content.OpenComponent<PolarisText>(4);
                content.AddAttribute(5, nameof(PolarisText.Color), PolarisColor.Subdued);
                content.AddAttribute(6, nameof(PolarisText.ChildContent), (RenderFragment)(text => text.AddContent(0, PageCountText())));
                content.CloseComponent();
            }
        }));
        builder.CloseComponent();
    }

    private void BuildControls(RenderTreeBuilder builder)
    {
        builder.OpenComponent<PolarisStack>(0);
        builder.AddAttribute(1, nameof(PolarisStack.Direction), PolarisStackDirection.Inline);
        builder.AddAttribute(2, nameof(PolarisStack.Gap), PolarisGap.Small);
        builder.AddAttribute(3, nameof(PolarisStack.AlignItems), "center");
        builder.AddAttribute(4, nameof(PolarisStack.ChildContent), (RenderFragment)(content =>
        {
            if (ShowPageSize)
            {
                BuildPageSizeSelect(content);
            }

            if (ShowFirstLast)
            {
                BuildStepButton(content, label: "First page", icon: "chevron-left", enabled: State.HasPreviousPage, move: () => State.SetPage(0));
            }

            BuildStepButton(content, label: "Previous page", icon: "chevron-left", enabled: State.HasPreviousPage, move: () => State.SetPage(State.Query.PageIndex - 1));

            if (ShowPageNumbers)
            {
                BuildPageNumbers(content);
            }

            BuildStepButton(content, label: "Next page", icon: "chevron-right", enabled: State.HasNextPage, move: () => State.SetPage(State.Query.PageIndex + 1));

            if (ShowFirstLast)
            {
                BuildStepButton(content, label: "Last page", icon: "chevron-right", enabled: State.HasNextPage, move: () => State.SetPage(State.PageCount - 1));
            }
        }));
        builder.CloseComponent();
    }

    private void BuildPageSizeSelect(RenderTreeBuilder builder)
    {
        builder.OpenComponent<PolarisSelect>(0);
        builder.AddAttribute(1, nameof(PolarisSelect.Label), "Rows per page");
        builder.AddAttribute(2, nameof(PolarisSelect.LabelAccessibilityVisibility), PolarisAccessibilityVisibility.Exclusive);
        builder.AddAttribute(3, nameof(PolarisSelect.Value), State.Query.PageSize.ToString(CultureInfo.InvariantCulture));
        builder.AddAttribute(4, nameof(PolarisSelect.OnChange), EventCallback.Factory.Create<PolarisEventArgs>(this, PageSizeChangedAsync));
        builder.AddAttribute(5, nameof(PolarisSelect.ChildContent), (RenderFragment)BuildPageSizeOptions);
        builder.CloseComponent();
    }

    private void BuildPageSizeOptions(RenderTreeBuilder builder)
    {
        foreach (var size in PageSizes())
        {
            builder.OpenComponent<PolarisOption>(0);
            builder.AddAttribute(1, nameof(PolarisOption.Value), size.ToString(CultureInfo.InvariantCulture));
            builder.AddAttribute(2, nameof(PolarisOption.Selected), size == State.Query.PageSize);
            builder.AddAttribute(3, nameof(PolarisOption.ChildContent), (RenderFragment)(text => text.AddContent(0, size.ToString(CultureInfo.InvariantCulture))));
            builder.CloseComponent();
        }
    }

    private void BuildPageNumbers(RenderTreeBuilder builder)
    {
        foreach (var page in PageNumberWindow())
        {
            if (page is null)
            {
                // An ellipsis is not a button: the pages it stands for are reachable with next, first
                // and last, so making it clickable would offer a jump the labels do not describe.
                builder.OpenComponent<PolarisText>(0);
                builder.AddAttribute(1, nameof(PolarisText.Color), PolarisColor.Subdued);
                builder.AddAttribute(2, nameof(PolarisText.ChildContent), (RenderFragment)(text => text.AddContent(0, "…")));
                builder.CloseComponent();
                continue;
            }

            var number = page.Value + 1;
            var current = page.Value == State.Query.PageIndex;

            builder.OpenComponent<PolarisButton>(10);
            builder.AddAttribute(11, nameof(PolarisButton.Variant), current ? PolarisButtonVariant.Secondary : PolarisButtonVariant.Tertiary);
            builder.AddAttribute(12, nameof(PolarisButton.AccessibilityLabel), current
                ? string.Create(CultureInfo.InvariantCulture, $"Page {number}, current page")
                : string.Create(CultureInfo.InvariantCulture, $"Page {number}"));
            builder.AddAttribute(13, nameof(PolarisButton.OnClick), EventCallback.Factory.Create<PolarisEventArgs>(this, _ => GoToPageAsync(page.Value)));
            builder.AddAttribute(14, nameof(PolarisButton.ChildContent), (RenderFragment)(text => text.AddContent(0, number.ToString(CultureInfo.InvariantCulture))));
            builder.CloseComponent();
        }
    }

    private void BuildStepButton(RenderTreeBuilder builder, string label, string icon, bool enabled, Action move)
    {
        builder.OpenComponent<PolarisButton>(20);
        builder.AddAttribute(21, nameof(PolarisButton.Variant), PolarisButtonVariant.Tertiary);
        builder.AddAttribute(22, nameof(PolarisButton.Icon), icon);
        builder.AddAttribute(23, nameof(PolarisButton.AccessibilityLabel), label);
        builder.AddAttribute(24, nameof(PolarisButton.Disabled), !enabled);
        builder.AddAttribute(25, nameof(PolarisButton.OnClick), EventCallback.Factory.Create<PolarisEventArgs>(this, _ => StepAsync(move)));
        builder.CloseComponent();
    }

    private string RangeText()
    {
        if (State.TotalCount == 0)
        {
            return "No records";
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{State.FirstRecordNumber}–{State.LastRecordNumber} of {State.TotalCount}");
    }

    private string PageCountText()
        => string.Create(CultureInfo.InvariantCulture, $"Page {State.Query.PageIndex + 1} of {State.PageCount}");

    private List<int?> PageNumberWindow()
    {
        var count = State.PageCount;
        var pages = new List<int?>(MaxPageNumbers + 2);

        if (count <= 0)
        {
            return pages;
        }

        var window = Math.Clamp(MaxPageNumbers, 3, 9);

        if (count <= window)
        {
            for (var index = 0; index < count; index++)
            {
                pages.Add(index);
            }

            return pages;
        }

        // The first and the last page keep their buttons, because they are the two jumps a long list
        // actually needs; the numbered buttons in between follow the current page. Reserving two of
        // the window's slots is what stops "5 at most" from quietly meaning six.
        var current = State.Query.PageIndex;
        var inner = window - 2;
        var start = Math.Clamp(current - (inner / 2), 1, Math.Max(1, count - 1 - inner));
        var end = Math.Min(count - 2, start + inner - 1);

        pages.Add(0);

        if (start > 1)
        {
            pages.Add(null);
        }

        for (var index = start; index <= end; index++)
        {
            pages.Add(index);
        }

        if (end < count - 2)
        {
            pages.Add(null);
        }

        pages.Add(count - 1);
        return pages;
    }

    private IEnumerable<int> PageSizes()
    {
        // The state's own size is always offered, even when the caller's list omits it: otherwise the
        // select has no selected option and shows the first one, describing a page size that is not
        // the one in force.
        var sizes = new SortedSet<int>(PageSizeOptions);

        if (State.Query.PageSize > 0)
        {
            sizes.Add(State.Query.PageSize);
        }

        return sizes;
    }

    private Task PageSizeChangedAsync(PolarisEventArgs args)
    {
        if (int.TryParse(args.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var size) && size > 0)
        {
            State.SetPageSize(size);
        }

        return Task.CompletedTask;
    }

    private Task GoToPageAsync(int pageIndex)
    {
        State.SetPage(pageIndex);
        return Task.CompletedTask;
    }

    private Task StepAsync(Action move)
    {
        move();
        return Task.CompletedTask;
    }
}

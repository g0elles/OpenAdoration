using Microsoft.Extensions.Logging.Abstractions;
using OpenAdoration.Application.Common;
using OpenAdoration.Application.Services;
using Xunit;

namespace OpenAdoration.Tests.Infrastructure.Projection;

/// <summary>
/// F2 follow-up: clicking Next (or the dedicated "Next Item" control) after a standalone
/// song/media item's slides are exhausted must actually advance into the next item —
/// mirroring the preview-only hint F2 added with real cross-item advancement, without
/// depending on the transient page VM that F1 disposes right after projecting.
/// Service-schedule behavior (ServiceScheduleViewModel's own NextScheduleItemRequested
/// handling) must be provably unchanged — asserted here via IsServiceScheduleActive gating.
/// </summary>
public sealed class ProjectionServiceTests
{
    private static ProjectionService MakeService() => new(NullLogger<ProjectionService>.Instance);

    private static Slide MakeSlide(string label) => new(label, SlideType.Song, label);

    [Fact]
    public void Next_AtLastSlide_WithStandaloneNextItemSet_AdvancesAndClearsState()
    {
        var service = MakeService();
        service.LoadSlides(new[] { MakeSlide("Song A - v1") }, "Song A");
        service.SetStandaloneNextItem(new[] { MakeSlide("Song B - v1"), MakeSlide("Song B - v2") }, "Song B");

        service.Next();

        Assert.Equal("Song B", service.ContextLabel);
        Assert.Equal("Song B - v1", service.CurrentSlide?.Label);
        Assert.Equal(2, service.CurrentSlides.Count);

        // Single-hop advance: the stored standalone-next state is consumed, not chained.
        service.Next();
        Assert.Equal("Song B - v2", service.CurrentSlide?.Label);
    }

    [Fact]
    public void Next_AtLastSlide_ServiceScheduleActive_IgnoresStandaloneNextItem()
    {
        var service = MakeService();
        service.LoadSlides(new[] { MakeSlide("Item A - v1") }, "Item A");
        service.SetStandaloneNextItem(new[] { MakeSlide("Item B - v1") }, "Item B");
        service.SetServiceScheduleActive(true);

        service.Next();

        // Existing service-mode boundary no-op must be unchanged even with a standalone-next
        // item present (shouldn't happen in practice given the VMs' own guards, but the
        // service-side check must still hold).
        Assert.Equal("Item A", service.ContextLabel);
        Assert.Equal("Item A - v1", service.CurrentSlide?.Label);
    }

    [Fact]
    public void Next_AtLastSlide_NoStandaloneNextItem_RemainsNoOp()
    {
        var service = MakeService();
        service.LoadSlides(new[] { MakeSlide("Song A - v1") }, "Song A");

        service.Next();

        Assert.Equal("Song A", service.ContextLabel);
        Assert.Equal("Song A - v1", service.CurrentSlide?.Label);
    }

    [Fact]
    public void RequestNextScheduleItem_StandaloneNextItemSet_AdvancesWithoutRaisingEvent()
    {
        var service = MakeService();
        service.LoadSlides(new[] { MakeSlide("Song A - v1") }, "Song A");
        service.SetStandaloneNextItem(new[] { MakeSlide("Song B - v1") }, "Song B");

        var raised = false;
        service.NextScheduleItemRequested += (_, _) => raised = true;

        service.RequestNextScheduleItem();

        Assert.Equal("Song B", service.ContextLabel);
        Assert.False(raised);
    }

    [Fact]
    public void RequestNextScheduleItem_ServiceScheduleActive_RaisesEventUnchanged()
    {
        var service = MakeService();
        service.LoadSlides(new[] { MakeSlide("Item A - v1") }, "Item A");
        service.SetServiceScheduleActive(true);

        var raised = false;
        service.NextScheduleItemRequested += (_, _) => raised = true;

        service.RequestNextScheduleItem();

        Assert.True(raised);
        Assert.Equal("Item A", service.ContextLabel);
    }

    [Fact]
    public void RequestNextScheduleItem_NoStandaloneNextItem_RaisesEventUnchanged()
    {
        var service = MakeService();
        service.LoadSlides(new[] { MakeSlide("Song A - v1") }, "Song A");

        var raised = false;
        service.NextScheduleItemRequested += (_, _) => raised = true;

        service.RequestNextScheduleItem();

        Assert.True(raised);
    }

    [Fact]
    public void SetStandaloneNextItem_NullSlides_ClearsPendingState()
    {
        var service = MakeService();
        service.LoadSlides(new[] { MakeSlide("Song A - v1") }, "Song A");
        service.SetStandaloneNextItem(new[] { MakeSlide("Song B - v1") }, "Song B");
        service.SetStandaloneNextItem(null, null);

        service.Next();

        Assert.Equal("Song A", service.ContextLabel);
    }
}

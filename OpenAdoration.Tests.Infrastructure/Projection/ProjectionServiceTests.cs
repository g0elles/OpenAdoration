using Microsoft.Extensions.Logging.Abstractions;
using OpenAdoration.Application.Common;
using OpenAdoration.Application.Services;
using Xunit;

namespace OpenAdoration.Tests.Infrastructure.Projection;

/// <summary>
/// Full-queue replacement for the earlier single-hop "standalone next item" design (which only
/// supported one forward hop, ever, and no backward movement — a real user could go A→B but never
/// back, or B→C). <see cref="ProjectionService.SetStandaloneQueue"/> stores the operator's whole
/// browsed list (e.g. Songs, or MediaViewModel.DisplayedFiles) so Next()/Previous() (and the
/// dedicated Stage View Next/Prev Item buttons) can hop freely across every item, in both
/// directions, any number of times. Service-schedule behavior must be provably unchanged — asserted
/// here via IsServiceScheduleActive gating.
/// </summary>
public sealed class ProjectionServiceTests
{
    private static ProjectionService MakeService() => new(NullLogger<ProjectionService>.Instance);

    private static Slide MakeSlide(string label) => new(label, SlideType.Song, label);

    private static StandaloneQueueItem MakeItem(string label, params string[] slideLabels) =>
        new(slideLabels.Select(MakeSlide).ToList(), label, null);

    [Fact]
    public void Next_MultiHop_AdvancesAcrossWholeQueue_NotJustOneItem()
    {
        var service = MakeService();
        var queue = new[]
        {
            MakeItem("Song A", "Song A - v1"),
            MakeItem("Song B", "Song B - v1"),
            MakeItem("Song C", "Song C - v1"),
        };
        service.LoadSlides(new[] { MakeSlide("Song A - v1") }, "Song A");
        service.SetStandaloneQueue(queue, 0);

        service.Next();
        Assert.Equal("Song B", service.ContextLabel);

        service.Next();
        Assert.Equal("Song C", service.ContextLabel);
    }

    [Fact]
    public void Previous_CrossingIntoPriorItem_LandsOnItsLastSlide_NotFirst()
    {
        var service = MakeService();
        var queue = new[]
        {
            MakeItem("Song A", "Song A - v1"),
            MakeItem("Song B", "Song B - v1", "Song B - v2"),
        };
        service.LoadSlides(new[] { MakeSlide("Song B - v1") }, "Song B");
        service.SetStandaloneQueue(queue, 1);

        // Song B's own first slide is index 0, so Previous() from there crosses into Song A —
        // landing on Song A's LAST (and only) slide since it has just one.
        service.Previous();

        Assert.Equal("Song A", service.ContextLabel);
        Assert.Equal("Song A - v1", service.CurrentSlide?.Label);
    }

    [Fact]
    public void Previous_MultiHop_GoesBackAcrossWholeQueue_NotJustOneItem()
    {
        var service = MakeService();
        var queue = new[]
        {
            MakeItem("Song A", "Song A - v1"),
            MakeItem("Song B", "Song B - v1"),
            MakeItem("Song C", "Song C - v1"),
        };
        service.LoadSlides(new[] { MakeSlide("Song C - v1") }, "Song C");
        service.SetStandaloneQueue(queue, 2);

        service.Previous();
        Assert.Equal("Song B", service.ContextLabel);

        service.Previous();
        Assert.Equal("Song A", service.ContextLabel);
        Assert.Equal("Song A - v1", service.CurrentSlide?.Label);
    }

    [Fact]
    public void Previous_LandsOnLastSlideOfMultiSlidePriorItem()
    {
        var service = MakeService();
        var queue = new[]
        {
            MakeItem("Song A", "Song A - v1", "Song A - v2", "Song A - v3"),
            MakeItem("Song B", "Song B - v1"),
        };
        service.LoadSlides(new[] { MakeSlide("Song B - v1") }, "Song B");
        service.SetStandaloneQueue(queue, 1);

        service.Previous();

        Assert.Equal("Song A", service.ContextLabel);
        Assert.Equal("Song A - v3", service.CurrentSlide?.Label);
    }

    [Fact]
    public void Next_AtLastQueueItem_FallsBackToExistingNoOp()
    {
        var service = MakeService();
        var queue = new[] { MakeItem("Song A", "Song A - v1"), MakeItem("Song B", "Song B - v1") };
        service.LoadSlides(new[] { MakeSlide("Song B - v1") }, "Song B");
        service.SetStandaloneQueue(queue, 1);

        service.Next();

        Assert.Equal("Song B", service.ContextLabel);
        Assert.Equal("Song B - v1", service.CurrentSlide?.Label);
    }

    [Fact]
    public void Previous_AtFirstQueueItem_FallsBackToExistingNoOp()
    {
        var service = MakeService();
        var queue = new[] { MakeItem("Song A", "Song A - v1"), MakeItem("Song B", "Song B - v1") };
        service.LoadSlides(new[] { MakeSlide("Song A - v1") }, "Song A");
        service.SetStandaloneQueue(queue, 0);

        service.Previous();

        Assert.Equal("Song A", service.ContextLabel);
        Assert.Equal("Song A - v1", service.CurrentSlide?.Label);
    }

    [Fact]
    public void Next_AtLastSlide_NoStandaloneQueueSet_RemainsNoOp()
    {
        var service = MakeService();
        service.LoadSlides(new[] { MakeSlide("Song A - v1") }, "Song A");

        service.Next();

        Assert.Equal("Song A", service.ContextLabel);
        Assert.Equal("Song A - v1", service.CurrentSlide?.Label);
    }

    [Fact]
    public void Next_AtLastSlide_ServiceScheduleActive_IgnoresStandaloneQueue()
    {
        var service = MakeService();
        var queue = new[] { MakeItem("Item A", "Item A - v1"), MakeItem("Item B", "Item B - v1") };
        service.LoadSlides(new[] { MakeSlide("Item A - v1") }, "Item A");
        service.SetStandaloneQueue(queue, 0);
        service.SetServiceScheduleActive(true);

        service.Next();

        // Existing service-mode boundary no-op must be unchanged even with a standalone queue
        // present (shouldn't happen in practice given the VMs' own guards, but the service-side
        // check must still hold).
        Assert.Equal("Item A", service.ContextLabel);
        Assert.Equal("Item A - v1", service.CurrentSlide?.Label);
    }

    [Fact]
    public void SetServiceScheduleActive_True_ClearsStaleStandaloneQueue()
    {
        var service = MakeService();
        var queue = new[] { MakeItem("Item A", "Item A - v1"), MakeItem("Item B", "Item B - v1") };
        service.LoadSlides(new[] { MakeSlide("Item A - v1") }, "Item A");
        service.SetStandaloneQueue(queue, 0);

        service.SetServiceScheduleActive(true);
        service.SetServiceScheduleActive(false);
        service.Next();

        // The queue was cleared when service mode turned on, so it's gone even after turning off.
        Assert.Equal("Item A", service.ContextLabel);
    }

    [Fact]
    public void RequestNextScheduleItem_StandaloneQueueSet_AdvancesWithoutRaisingEvent()
    {
        var service = MakeService();
        var queue = new[] { MakeItem("Song A", "Song A - v1"), MakeItem("Song B", "Song B - v1") };
        service.LoadSlides(new[] { MakeSlide("Song A - v1") }, "Song A");
        service.SetStandaloneQueue(queue, 0);

        var raised = false;
        service.NextScheduleItemRequested += (_, _) => raised = true;

        service.RequestNextScheduleItem();

        Assert.Equal("Song B", service.ContextLabel);
        Assert.False(raised);
    }

    [Fact]
    public void RequestPreviousScheduleItem_StandaloneQueueSet_MovesBackWithoutRaisingEvent()
    {
        var service = MakeService();
        var queue = new[] { MakeItem("Song A", "Song A - v1"), MakeItem("Song B", "Song B - v1") };
        service.LoadSlides(new[] { MakeSlide("Song B - v1") }, "Song B");
        service.SetStandaloneQueue(queue, 1);

        var raised = false;
        service.PreviousScheduleItemRequested += (_, _) => raised = true;

        service.RequestPreviousScheduleItem();

        Assert.Equal("Song A", service.ContextLabel);
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
    public void RequestNextScheduleItem_NoStandaloneQueue_RaisesEventUnchanged()
    {
        var service = MakeService();
        service.LoadSlides(new[] { MakeSlide("Song A - v1") }, "Song A");

        var raised = false;
        service.NextScheduleItemRequested += (_, _) => raised = true;

        service.RequestNextScheduleItem();

        Assert.True(raised);
    }

    [Fact]
    public void SetStandaloneQueue_EmptyItems_ClearsPendingQueue()
    {
        var service = MakeService();
        var queue = new[] { MakeItem("Song A", "Song A - v1"), MakeItem("Song B", "Song B - v1") };
        service.LoadSlides(new[] { MakeSlide("Song A - v1") }, "Song A");
        service.SetStandaloneQueue(queue, 0);
        service.SetStandaloneQueue(Array.Empty<StandaloneQueueItem>(), 0);

        service.Next();

        Assert.Equal("Song A", service.ContextLabel);
    }

    [Fact]
    public void Stop_ClearsStandaloneQueue()
    {
        var service = MakeService();
        var queue = new[] { MakeItem("Song A", "Song A - v1"), MakeItem("Song B", "Song B - v1") };
        service.LoadSlides(new[] { MakeSlide("Song A - v1") }, "Song A");
        service.SetStandaloneQueue(queue, 0);

        service.Stop();
        service.LoadSlides(new[] { MakeSlide("Song A - v1") }, "Song A");
        service.Next();

        Assert.Equal("Song A", service.ContextLabel);
    }
}

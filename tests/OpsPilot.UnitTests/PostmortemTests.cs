using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Exceptions;
using Xunit;

namespace OpsPilot.UnitTests;

/// <summary>
/// Covers the retrospective's state machine on its own, without a database: a draft is the
/// only thing that may be written, a review may be sent back, and only a reviewed document
/// may be published.
/// </summary>
public class PostmortemTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid IncidentId = Guid.NewGuid();
    private static readonly Guid AuthorId = Guid.NewGuid();

    [Fact]
    public void Create_Starts_As_A_Draft()
    {
        var postmortem = Postmortem.Create(TenantId, IncidentId, "Checkout outage", "Payments was down", AuthorId);

        Assert.Equal(TenantId, postmortem.TenantId);
        Assert.Equal(IncidentId, postmortem.IncidentId);
        Assert.Equal("Checkout outage", postmortem.Title);
        Assert.Equal("Payments was down", postmortem.Summary);
        Assert.Equal(PostmortemStatus.Draft, postmortem.Status);
        Assert.Equal(AuthorId, postmortem.CreatedByUserId);
        Assert.Null(postmortem.RootCause);
        Assert.Null(postmortem.ActionItems);
        Assert.Null(postmortem.PublishedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_With_Blank_Title_Throws(string title)
    {
        Assert.Throws<ArgumentException>(() =>
            Postmortem.Create(TenantId, IncidentId, title, "Payments was down", AuthorId));
    }

    [Fact]
    public void A_Draft_Can_Record_Its_Root_Cause_And_Action_Items()
    {
        var postmortem = A_Draft();

        postmortem.RecordRootCause("  A connection pool ran out  ");
        postmortem.RecordActionItems("Raise the pool ceiling");

        Assert.Equal("A connection pool ran out", postmortem.RootCause);
        Assert.Equal("Raise the pool ceiling", postmortem.ActionItems);
    }

    [Fact]
    public void Clearing_The_Root_Cause_Stores_Null_Rather_Than_Blanks()
    {
        var postmortem = A_Draft();

        postmortem.RecordRootCause("Something");
        postmortem.RecordRootCause("   ");

        Assert.Null(postmortem.RootCause);
    }

    [Fact]
    public void A_Draft_Can_Be_Rewritten()
    {
        var postmortem = A_Draft();

        postmortem.UpdateContent("Better title", "Better summary", "A cause", "Some actions");

        Assert.Equal("Better title", postmortem.Title);
        Assert.Equal("Better summary", postmortem.Summary);
        Assert.Equal("A cause", postmortem.RootCause);
        Assert.Equal("Some actions", postmortem.ActionItems);
    }

    [Fact]
    public void A_Draft_That_Is_Under_Review_Cannot_Be_Written_To()
    {
        var postmortem = A_Draft();

        postmortem.MoveToReview();

        Assert.Throws<InvalidStateTransitionException>(() =>
            postmortem.UpdateContent("Sneaky", "Sneaky", null, null));

        Assert.Throws<InvalidStateTransitionException>(() =>
            postmortem.RecordRootCause("Sneaky"));

        Assert.Throws<InvalidStateTransitionException>(() =>
            postmortem.RecordActionItems("Sneaky"));

        // The text under review is still the text under review.
        Assert.Equal("A draft", postmortem.Title);
    }

    [Fact]
    public void A_Published_Postmortem_Cannot_Be_Written_To()
    {
        var postmortem = A_Published();

        Assert.Throws<InvalidStateTransitionException>(() =>
            postmortem.UpdateContent("Sneaky", "Sneaky", null, null));
    }

    [Fact]
    public void A_Rejected_Review_Returns_The_Postmortem_To_Its_Draft()
    {
        var postmortem = A_Draft();

        postmortem.MoveToReview();
        postmortem.ReturnToDraft();

        Assert.Equal(PostmortemStatus.Draft, postmortem.Status);

        // And it can be written to again, which is the point of sending it back.
        postmortem.UpdateContent("Rewritten", "Rewritten", null, null);

        Assert.Equal("Rewritten", postmortem.Title);
    }

    [Fact]
    public void Only_A_Postmortem_Under_Review_Can_Be_Sent_Back()
    {
        var postmortem = A_Draft();

        Assert.Throws<InvalidStateTransitionException>(() => postmortem.ReturnToDraft());
    }

    [Fact]
    public void Only_A_Draft_Can_Be_Sent_For_Review()
    {
        var postmortem = A_Published();

        Assert.Throws<InvalidStateTransitionException>(() => postmortem.MoveToReview());
    }

    [Fact]
    public void Only_A_Postmortem_Under_Review_Can_Be_Published()
    {
        var postmortem = A_Draft();

        Assert.Throws<InvalidStateTransitionException>(() => postmortem.Publish(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Publishing_Stamps_The_Moment()
    {
        var postmortem = A_Draft();
        var at = DateTimeOffset.UtcNow;

        postmortem.MoveToReview();
        postmortem.Publish(at);

        Assert.Equal(PostmortemStatus.Published, postmortem.Status);
        Assert.Equal(at, postmortem.PublishedAt);
    }

    private static Postmortem A_Draft() =>
        Postmortem.Create(TenantId, IncidentId, "A draft", "A summary", AuthorId);

    private static Postmortem A_Published()
    {
        var postmortem = A_Draft();
        postmortem.MoveToReview();
        postmortem.Publish(DateTimeOffset.UtcNow);
        return postmortem;
    }
}

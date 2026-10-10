using Gdzie.Kupic.Domain.Model.Location;
using Gdzie.Kupic.Domain.Model.Marketplace;
using Shouldly;

namespace Gdzie.Kupic.Tests.Unit.Marketplace;

public class MerchantResponseTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private static Post NewPost() =>
        new(Guid.NewGuid(), Guid.NewGuid(), new Coordinates(50, 19), 5m, Guid.NewGuid(), Guid.NewGuid(), "Title", null,
            null, Now.AddHours(72), Now);

    private static MerchantResponse NewResponse(ResponseState state = ResponseState.MayHaveIt) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), state, Now);

    [TestCase(ResponseState.CantHelp, false)]
    [TestCase(ResponseState.MayHaveIt, true)]
    [TestCase(ResponseState.HaveIt, true)]
    [TestCase(ResponseState.CanOrderIt, true)]
    public void IsPositive_OnlyCantHelpIsNegative(ResponseState state, bool expected) =>
        state.IsPositive().ShouldBe(expected);

    [Test]
    public void TryChangeState_AnyStateToAnyOther_WhilePostIsOpen()
    {
        var states = Enum.GetValues<ResponseState>();

        foreach (var from in states)
        foreach (var to in states)
        {
            var response = NewResponse(from);

            response.TryChangeState(to, NewPost(), Now.AddMinutes(5)).ShouldBeTrue();

            response.State.ShouldBe(to);
            response.UpdatedAt.ShouldBe(Now.AddMinutes(5));
        }
    }

    [Test]
    public void TryChangeState_PastExpiry_IsRejectedAndKeepsState()
    {
        var response = NewResponse(ResponseState.HaveIt);
        var post = NewPost();

        response.TryChangeState(ResponseState.CantHelp, post, post.ExpiresAt).ShouldBeFalse();

        response.State.ShouldBe(ResponseState.HaveIt);
        response.UpdatedAt.ShouldBe(Now);
    }

    [Test]
    public void TryChangeState_ClosedPost_IsRejected()
    {
        var response = NewResponse();
        var post = NewPost();
        post.TryClose(Now.AddMinutes(1)).ShouldBeTrue();

        response.TryChangeState(ResponseState.HaveIt, post, Now.AddMinutes(2)).ShouldBeFalse();

        response.State.ShouldBe(ResponseState.MayHaveIt);
    }
}
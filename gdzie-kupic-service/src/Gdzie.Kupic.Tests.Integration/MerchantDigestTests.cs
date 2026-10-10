using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gdzie.Kupic.Auth;
using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Domain.Model.Catalogue;
using Gdzie.Kupic.Domain.Model.Common;
using Gdzie.Kupic.Domain.Model.Location;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Gdzie.Kupic.Domain.Model.Marketplace;
using Gdzie.Kupic.Domain.Model.Notifications;
using Gdzie.Kupic.Marketplace;
using Gdzie.Kupic.Notifications;
using Gdzie.Kupic.Realtime;
using Gdzie.Kupic.Service.API.Contract.Merchant;
using Gdzie.Kupic.Service.API.Contract.Realtime;
using Gdzie.Kupic.Storage;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Gdzie.Kupic.Tests.Integration;

public class MerchantDigestTests : IntegrationTestBase
{
    private Guid _buyerId;
    private Guid _merchantUser1;
    private Guid _merchantUser2;
    private Guid _merchantId;
    private Guid _postId;
    
    
    

    private static RecordingJobScheduler Scheduler => IntegrationTestSetup.Factory.Services.GetRequiredService<RecordingJobScheduler>();
    private static RecordingEmailSender Mail => IntegrationTestSetup.Factory.Services.GetRequiredService<RecordingEmailSender>();

    private Guid _merchantB;
    private Guid _bannedUser;
    private Guid _optedOutUser;
    private Guid _bannedMerchantUser;
    private Category _category = null!;
    private Tag _tag = null!;

    [SetUp]
    public async Task Seed()
    {
        _category = new Category(Guid.NewGuid(), "Audio", false, DateTimeOffset.UtcNow);
        _tag = new Tag(Guid.NewGuid(), _category.Id, "Microphones", false, DateTimeOffset.UtcNow);
        _buyerId = await AuthenticateAsync(Role.Buyer);
        _merchantUser1 = await AuthenticateAsync(Role.Merchant);
        _merchantUser2 = await AuthenticateAsync(Role.Merchant);
        _optedOutUser = await AuthenticateAsync(Role.Merchant);
        _bannedUser = await AuthenticateAsync(Role.Merchant);
        _bannedMerchantUser = await AuthenticateAsync(Role.Merchant);
        var zeroUser = await AuthenticateAsync(Role.Merchant);

        _merchantId = Guid.NewGuid();
        _merchantB = Guid.NewGuid();
        var bannedMerchant = Guid.NewGuid();
        await WithDbAsync(async db =>
        {
            db.Categories.Add(_category);
            db.Tags.Add(_tag);
            db.Merchants.AddRange(
                new Merchant(_merchantId, "Shop", null, DateTimeOffset.UtcNow),
                new Merchant(_merchantB, "Empty", null, DateTimeOffset.UtcNow),
                new Merchant(bannedMerchant, "Banned", null, DateTimeOffset.UtcNow));
            db.Entry(await db.Merchants.FindAsync(bannedMerchant) ?? throw new InvalidOperationException())
                .Reference(m => m.BanDetails).CurrentValue = new BanDetails(DateTimeOffset.UtcNow);
            db.MerchantAccounts.AddRange(
                new MerchantAccount(Guid.NewGuid(), _merchantId, _merchantUser1, DateTimeOffset.UtcNow),
                new MerchantAccount(Guid.NewGuid(), _merchantId, _merchantUser2, DateTimeOffset.UtcNow),
                new MerchantAccount(Guid.NewGuid(), _merchantId, _optedOutUser, DateTimeOffset.UtcNow),
                new MerchantAccount(Guid.NewGuid(), _merchantId, _bannedUser, DateTimeOffset.UtcNow),
                new MerchantAccount(Guid.NewGuid(), bannedMerchant, _bannedMerchantUser, DateTimeOffset.UtcNow),
                new MerchantAccount(Guid.NewGuid(), _merchantB, zeroUser, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();

            foreach (var id in new[] { _merchantUser1, _merchantUser2, _bannedUser, _bannedMerchantUser, zeroUser })
                (await db.Users.SingleAsync(u => u.Id == id)).EmailNotificationsEnabled = true;
            db.Entry(await db.Users.SingleAsync(u => u.Id == _bannedUser)).Reference(u => u.BanDetails).CurrentValue = new BanDetails(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        });
        await AddPostAsync(notifiedMerchant: _merchantId);
        await AddPostAsync(notifiedMerchant: _merchantId);
        await AddPostAsync(notifiedMerchant: bannedMerchant);
    }

    private async Task<Guid> AddPostAsync(Guid notifiedMerchant, DateTimeOffset? expiresAt = null)
    {
        var id = Guid.NewGuid();
        await WithDbAsync(async db =>
        {
            db.Posts.Add(new Post(id, _buyerId, new Coordinates(50, 19), 5m, _category.Id, _tag.Id, "Need", null, null,
                expiresAt ?? DateTimeOffset.UtcNow.AddDays(3), DateTimeOffset.UtcNow));
            db.PostNotifications.Add(new PostNotification(Guid.NewGuid(), id, notifiedMerchant, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        });
        return id;
    }

    private static async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = IntegrationTestSetup.Factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private static async Task RunDigestAsync()
    {
        using var scope = IntegrationTestSetup.Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<MerchantDigestJob>().RunAsync();
    }

    [Test]
    public async Task SendsOneDigestToEveryOptedInNonBannedAccount_WithTheFeedNewCounter()
    {
        await RunDigestAsync();

        var recipients = await EmailsOfAsync(_merchantUser1, _merchantUser2);
        Mail.Sent.Select(m => m.To).ShouldBe(recipients, ignoreOrder: true);
        Mail.Sent.ShouldAllBe(m => m.Body.Contains("czekaj\u0105cych na Twoj\u0105 odpowied\u017a: 2"));
        Mail.Sent.ShouldAllBe(m => m.Body.Contains("https://app.example.com/feed") && m.Body.Contains("/settings/notifications"));
    }

    [Test]
    public async Task CounterMatchesTheFeed_RespondedAndExpiredRequestsAreNotCounted()
    {
        await WithDbAsync(async db =>
        {
            var first = await db.PostNotifications.Where(n => n.MerchantId == _merchantId).Select(n => n.PostId).FirstAsync();
            db.MerchantResponses.Add(new MerchantResponse(Guid.NewGuid(), first, _merchantId, ResponseState.CantHelp, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        });
        await AddPostAsync(_merchantId, DateTimeOffset.UtcNow.AddMinutes(-5));

        await RunDigestAsync();

        Mail.Sent.ShouldAllBe(m => m.Body.Contains("odpowied\u017a: 1."));
    }

    [Test]
    public async Task ZeroUnprocessed_SendsNothing()
    {
        await WithDbAsync(async db =>
        {
            foreach (var n in await db.PostNotifications.Where(n => n.MerchantId == _merchantId).ToListAsync())
                db.MerchantResponses.Add(new MerchantResponse(Guid.NewGuid(), n.PostId, _merchantId, ResponseState.HaveIt, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        });

        await RunDigestAsync();

        Mail.Sent.ShouldBeEmpty();
    }

    [Test]
    public async Task ReRunInTheSameSlot_SendsNoDuplicate()
    {
        await RunDigestAsync();
        await RunDigestAsync();

        Mail.Sent.Count.ShouldBe(2);
    }

    [Test]
    public async Task FailureForOneRecipient_DoesNotStopTheOthers_AndTheRetryOnlyCoversTheFailed()
    {
        var failing = (await EmailsOfAsync(_merchantUser1)).Single();
        Mail.FailWhen = m => m.To == failing;

        await Should.ThrowAsync<InvalidOperationException>(RunDigestAsync);
        Mail.Sent.Count.ShouldBe(1);

        Mail.FailWhen = null;
        await RunDigestAsync();

        Mail.Sent.Select(m => m.To).ShouldBe(await EmailsOfAsync(_merchantUser1, _merchantUser2), ignoreOrder: true);
    }

    [Test]
    public async Task IsSentRegardlessOfPresence()
    {
        var presence = IntegrationTestSetup.Factory.Services.GetRequiredService<ConnectionPresenceTracker>();
        presence.Connected(_merchantUser1, "c1");
        try
        {
            await RunDigestAsync();
        }
        finally
        {
            presence.Disconnected(_merchantUser1, "c1");
        }

        Mail.Sent.Count.ShouldBe(2);
    }

    [Test]
    public void IsRegisteredWithTheConfiguredScheduleAndZone()
    {
        var scheduler = IntegrationTestSetup.Factory.Services.GetRequiredService<RecordingJobScheduler>();

        var (cron, zone) = scheduler.Recurring["merchant-digest"];

        cron.ShouldBe("0 9 * * *");
        zone!.Id.ShouldBe("Europe/Warsaw");
    }

    private static async Task<List<string>> EmailsOfAsync(params Guid[] userIds)
    {
        var emails = new List<string>();
        await WithDbAsync(async db => emails = await db.Users.Where(u => userIds.Contains(u.Id)).Select(u => u.Email).ToListAsync());
        return emails;
    }
}

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore.Communication;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationReplyPageQuery_Tests(NotificationPostgresFixture fixture) : IClassFixture<NotificationPostgresFixture>
{
	[NotificationPostgresFact]
	public async Task Native_postgres_pages_preserve_the_filtered_audience_and_received_instant_order()
	{
		var tenant = Guid.NewGuid(); var now = DateTimeOffset.UtcNow; await using var db = fixture.CreateContext();
		var first = Row(tenant, now.AddMinutes(-2)); var second = Row(tenant, now.AddMinutes(-1)); var third = Row(tenant, now);
		db.AddRange(first, second, third, Row(Guid.NewGuid(), now.AddHours(1))); await db.SaveChangesAsync();
		await using var context = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>().UseNpgsql(fixture.CreateConnection()).Options);
		var provider = Substitute.For<IDbContextProvider<ShikshaMitraDbContext>>(); provider.GetDbContextAsync().Returns(context);
		var reader = new CommunicationReplyPageQuery(provider); var query = db.Set<CommunicationReply>().Where(x => x.TenantId == tenant);
		var page = await reader.GetPageAsync(query, 0, 2); page.Select(x => x.Id).ShouldBe(new[] { third.Id, second.Id });
		(await reader.GetPageAsync(query, 2, 2)).Select(x => x.Id).ShouldBe(new[] { first.Id });
	}
	[NotificationPostgresFact]
	public async Task Invalid_page_bounds_do_not_execute_an_unbounded_query()
	{
		await using var db = fixture.CreateContext(); var provider = Substitute.For<IDbContextProvider<ShikshaMitraDbContext>>();
		var reader = new CommunicationReplyPageQuery(provider);
		await Should.ThrowAsync<ArgumentOutOfRangeException>(() => reader.GetPageAsync(db.Set<CommunicationReply>(), -1, 1));
		await Should.ThrowAsync<ArgumentOutOfRangeException>(() => reader.GetPageAsync(db.Set<CommunicationReply>(), 0, 0));
		await Should.ThrowAsync<ArgumentOutOfRangeException>(() => reader.GetPageAsync(db.Set<CommunicationReply>(), 0, 1001));
	}
	private static CommunicationReply Row(Guid tenant, DateTimeOffset receivedAt) => new(Guid.NewGuid(), tenant) {
		Body = "Synthetic reply", FromAddress = "synthetic@example.test", ReceivedAt = receivedAt
	};
}

using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore.Communication.Notifications;
using Shouldly;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationQueueRepository_Tests
{
	[Fact]
	public async Task Host_context_cannot_claim_every_schools_notifications()
	{
		var contexts = Substitute.For<IDbContextProvider<ShikshaMitraDbContext>>();
		var tenant = Substitute.For<ICurrentTenant>();
		tenant.Id.Returns((Guid?)null);
		var repository = new NotificationQueueRepository(contexts, tenant);
		var error = await Should.ThrowAsync<BusinessException>(() => repository.ClaimIntentsAsync(
			DateTimeOffset.UtcNow, 100, TimeSpan.FromMinutes(5), CancellationToken.None));
		error.Code.ShouldBe(NotificationErrorCodes.TenantRequired);
		await contexts.DidNotReceive().GetDbContextAsync();
	}
}

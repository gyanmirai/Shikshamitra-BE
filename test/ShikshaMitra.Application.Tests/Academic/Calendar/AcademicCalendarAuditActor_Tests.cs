using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Academic.Calendar;

public class AcademicCalendarAuditActor_Tests
{
	[Fact]
	public async Task Batch_Actor_Should_Resolve_Once_And_Keep_Audit_Record_Per_Change()
	{
		var tenantId = Guid.NewGuid();
		var actorId = Guid.NewGuid();
		var repository = Substitute.For<IRepository<AcademicCalendarAuditLog, Guid>>();
		var users = Substitute.For<IIdentityUserRepository>();
		var roles = Substitute.For<IRepository<IdentityRole, Guid>>();
		var currentTenant = Substitute.For<ICurrentTenant>();
		var currentUser = Substitute.For<ICurrentUser>();
		var generator = Substitute.For<IGuidGenerator>();
		currentTenant.Id.Returns(tenantId);
		currentUser.Id.Returns(actorId);
		generator.Create().Returns(_ => Guid.NewGuid());
		var role = new IdentityRole(Guid.NewGuid(), "Principal", tenantId);
		var user = new IdentityUser(actorId, "calendar-manager", "manager@example.test", tenantId);
		user.AddRole(role.Id);
		users.FindAsync(actorId, true, Arg.Any<CancellationToken>()).Returns(user);
		roles.GetListAsync(Arg.Any<Expression<Func<IdentityRole, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
			.Returns(new List<IdentityRole> { role });
		var written = new List<AcademicCalendarAuditLog>();
		repository.InsertAsync(Arg.Any<AcademicCalendarAuditLog>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
			.Returns(call =>
			{
				var log = call.ArgAt<AcademicCalendarAuditLog>(0);
				written.Add(log);
				return log;
			});
		var writer = new AcademicCalendarAuditWriter(repository, users, roles, new HttpContextAccessor(),
			currentTenant, currentUser, generator);
		var actor = await writer.CaptureActorAsync();
		var calendarId = Guid.NewGuid();

		await writer.WriteAsync(calendarId, CalendarAuditAction.DayAdded, null, "{\"title\":\"First day\"}", actor: actor);
		await writer.WriteAsync(calendarId, CalendarAuditAction.DayAdded, null, "{\"title\":\"Second day\"}", actor: actor);

		await users.Received(1).FindAsync(actorId, true, Arg.Any<CancellationToken>());
		await roles.Received(1).GetListAsync(Arg.Any<Expression<Func<IdentityRole, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
		written.Count.ShouldBe(2);
		written[0].Id.ShouldNotBe(written[1].Id);
		written.ShouldAllBe(log => log.TenantId == tenantId && log.ChangedByUserId == actorId &&
			log.ActorNameSnapshot == "calendar-manager" && log.ActorRoleSnapshot == "Principal" &&
			log.ActorEmailSnapshot == "manager@example.test");
	}
}

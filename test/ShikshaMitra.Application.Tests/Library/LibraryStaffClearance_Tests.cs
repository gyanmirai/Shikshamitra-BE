using System;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Clearance;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryStaffClearance_Tests : ShikshaMitraApplicationTestBase<LibraryStockVerificationTestModule>
{
	[Fact]
	public async Task Staff_Clearance_Should_Require_Library_View_And_Distinguish_Scoped_Evidence_From_A_School_Total()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var employee = Guid.NewGuid();
		var tenant = GetRequiredService<ICurrentTenant>().Id;
		var actor = GetRequiredService<ICurrentUser>().Id!.Value;
		var permission = ShikshaMitraSubmodulePermissions.Library.LibraryMembers.View;
		var permissions = GetRequiredService<IPermissionChecker>();
		await WithUnitOfWorkAsync(async () =>
		{
			var issues = GetRequiredService<IRepository<BookIssue, Guid>>();
			var day = new DateTime(2025, 11, 1, 0, 0, 0, DateTimeKind.Utc);
			await issues.InsertAsync(new(Guid.NewGuid(), Guid.NewGuid(), "STAFF-OPEN", employee, 1, day, day.AddDays(7), tenant), true);
			await issues.InsertAsync(new(Guid.NewGuid(), Guid.NewGuid(), "STUDENT-COLLISION", employee, 0, day, day.AddDays(7), tenant), true);
			await issues.InsertAsync(new(Guid.NewGuid(), Guid.NewGuid(), "STAFF-RETURNED", employee, 1, day, day.AddDays(7), tenant)
				{ Status = BookIssueStatus.Returned }, true);
		});
		var reader = GetRequiredService<LibraryStaffClearanceReader>();
		(await WithUnitOfWorkAsync(() => reader.ReadAsync(employee))).ShouldBe(new("Restricted", null));
		permissions.IsGrantedAsync(permission).Returns(true);
		(await WithUnitOfWorkAsync(() => reader.ReadAsync(employee))).ShouldBe(new("Pending", 1));
		(await WithUnitOfWorkAsync(() => reader.ReadAsync(Guid.NewGuid()))).ShouldBe(new("Clear", 0));
		await WithUnitOfWorkAsync(async () =>
		{
			var scopes = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
			await scopes.DeleteAsync(row => row.TenantId == tenant && row.UserId == actor, true);
			await scopes.InsertAsync(new(Guid.NewGuid(), tenant, userId: actor, academicYearId: context.YearId,
				description: "Assigned current academic year"), true);
		});
		await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(actor);
		(await WithUnitOfWorkAsync(() => reader.ReadAsync(employee))).ShouldBe(new("Limited", 0));
	}
}

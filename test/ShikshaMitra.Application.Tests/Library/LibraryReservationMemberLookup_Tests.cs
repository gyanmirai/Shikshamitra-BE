using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryReservationMemberLookup_Tests : ShikshaMitraApplicationTestBase<LibraryReservationMemberLookupTestModule>
{
	[Fact]
	public async Task Reserve_Only_Operator_Should_Search_And_Resolve_Printed_Cards_Without_Member_View()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await ContextAsync();
		(await Permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.LibraryMembers.View)).ShouldBeFalse();

		var list = await Members.SearchAsync(context.Catalog.YearId, "Borrower");
		var resolved = await Members.ResolveAsync(context.Catalog.YearId, "  CARD-READY  ");

		list.TotalCount.ShouldBe(1);
		list.Items.Single().Id.ShouldBe(context.StudentId);
		resolved.Id.ShouldBe(context.StudentId);
		resolved.MemberName.ShouldBe("Library Borrower");
		resolved.MemberIdentifier.ShouldBe("CARD-READY");
		resolved.Role.ShouldBe(0);
		resolved.ClassDivision.ShouldBe("5");
		resolved.IsEligibleForReservation.ShouldBeTrue();
	}

	[Theory]
	[InlineData(ShikshaMitraSubmodulePermissions.Library.AllBooks.View)]
	[InlineData(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Reserve)]
	public async Task Reservation_Borrower_Lookup_Should_Require_Each_Owning_Grant(string deniedGrant)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await ContextAsync();
		Permissions.IsGrantedAsync(deniedGrant).Returns(false);
		(await Permissions.IsGrantedAsync(deniedGrant)).ShouldBeFalse();

		await Should.ThrowAsync<AbpAuthorizationException>(() => Members.SearchAsync(context.Catalog.YearId));
		await Should.ThrowAsync<AbpAuthorizationException>(() => Members.ResolveAsync(context.Catalog.YearId, "CARD-READY"));
	}

	[Fact]
	public async Task Reservation_Borrower_Search_And_Exact_Resolve_Should_Enforce_Current_Year_Tenant_And_Class_Scope()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await ContextAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			var student = Student.CreateDirectRegistration(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id, "Hidden", null, "Borrower",
				context.Catalog.YearId, context.BoardId, context.MediumId, context.StageId, context.OtherClassId, null,
				new DateTimeOffset(2014, 1, 2, 0, 0, 0, TimeSpan.Zero), GenderEnum.Male, "8888888888");
			student.AdditionalDetail!.IsLibraryMember = true;
			await GetRequiredService<IRepository<Student, Guid>>().InsertAsync(student, true);
			await GetRequiredService<IRepository<LibraryMember, Guid>>().InsertAsync(new(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id,
				student.Id, "CARD-HIDDEN", true), true);
		});
		using (LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider)) await ContextAsync();
		await RestrictAsync(context);

		var list = await Members.SearchAsync(context.Catalog.YearId, "Borrower");

		list.TotalCount.ShouldBe(1);
		list.Items.Single().Id.ShouldBe(context.StudentId);
		(await Members.ResolveAsync(context.Catalog.YearId, "CARD-READY")).Id.ShouldBe(context.StudentId);
		await Should.ThrowAsync<BusinessException>(() => Members.ResolveAsync(context.Catalog.YearId, "CARD-HIDDEN"));
		await Should.ThrowAsync<BusinessException>(() => Members.SearchAsync(Guid.NewGuid()));
	}

	[Fact]
	public async Task Viewing_An_Existing_Request_Should_Hydrate_Actual_Member_Labels_Without_Reserve_Or_Member_View()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await ContextAsync();
		var request = BookReservation.Create(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id, context.Catalog.BookId,
			context.StudentId, context.StudentId, 0, context.Catalog.YearId, 1, new(2026, 11, 17), new(2026, 12, 1), "{}");
		await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<BookReservation, Guid>>().InsertAsync(request, true));
		Permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Reserve).Returns(false);

		var result = await GetRequiredService<ILibraryReservationAppService>().GetAsync(request.Id, context.Catalog.YearId);

		result.MemberName.ShouldBe("Library Borrower");
		result.MemberIdentifier.ShouldBe("CARD-READY");
		result.ClassDivision.ShouldBe("5");
		result.Role.ShouldBe(0);
		result.CanCancel.ShouldBeFalse();
	}

	private ILibraryReservationMemberAppService Members => GetRequiredService<ILibraryReservationMemberAppService>();
	private IPermissionChecker Permissions => GetRequiredService<IPermissionChecker>();
	private Task<LibraryCopyLifecycleTestContext> ContextAsync() => WithUnitOfWorkAsync(async () =>
	{
		var context = await LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider);
		await GetRequiredService<IRepository<LibraryMember, Guid>>().InsertAsync(new(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id,
			context.StudentId, "CARD-READY", true), true);
		return context;
	});
	private Task RestrictAsync(LibraryCopyLifecycleTestContext context) => WithUnitOfWorkAsync(async () =>
	{
		var userId = GetRequiredService<ICurrentUser>().Id!.Value;
		var scopes = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		await scopes.DeleteAsync(row => row.UserId == userId, true);
		await scopes.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id, userId: userId,
			academicYearId: context.Catalog.YearId, educationBoardId: context.BoardId, educationMediumId: context.MediumId,
			educationMediumStageId: context.StageId, standardId: context.BorrowerClassId), true);
		await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(userId);
	});
}

[DependsOn(typeof(ShikshaMitraApplicationTestModule))]
public class LibraryReservationMemberLookupTestModule : AbpModule
{
	public override void ConfigureServices(ServiceConfigurationContext context)
	{
		var permissions = Substitute.For<IPermissionChecker>();
		permissions.IsGrantedAsync(Arg.Any<string>()).Returns(true);
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.LibraryMembers.View).Returns(false);
		context.Services.Replace(ServiceDescriptor.Singleton(permissions));
	}
}

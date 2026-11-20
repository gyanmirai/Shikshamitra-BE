using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Volo.Abp.Timing;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryCanonicalLookupCalendar_Tests : ShikshaMitraApplicationTestBase<LibraryCanonicalLookupCalendarTestModule>
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Owning_Circulation_Lookup_Should_Default_To_The_Confirmed_School_Day_Ahead_Of_Utc_Without_Admin_Grants(bool returning)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync(returning);
		await SetZoneAsync(LibraryPolicySettingNames.CalendarTimeZone, "Asia/Kolkata");

		var result = await ReadContextAsync(context, returning);

		result.SchoolToday.ShouldBe(new DateOnly(2026, 11, 18));
		result.ConfirmedTimeZoneId.ShouldBe("Asia/Kolkata");
		result.IsConfirmed.ShouldBeTrue();
		result.RequiresExplicitDate.ShouldBeFalse();
		result.UnavailableReason.ShouldBeNull();
		result.AcademicYears.Select(row => row.Id).ShouldContain(context.Catalog.YearId);
		result.AcademicYears.ShouldAllBe(row => !string.IsNullOrWhiteSpace(row.Name));
		if (returning) await WithUnitOfWorkAsync(async () =>
		{
			var loan = await GetRequiredService<IRepository<BookIssue, Guid>>().GetAsync(context.LoanId!.Value);
			loan.SchoolIssueDate.ShouldBeNull();
			loan.SchoolDueDate.ShouldBeNull();
			((int)loan.IssueDateProvenance).ShouldBe(0);
			((int)loan.DueDateProvenance).ShouldBe(0);
			loan.IssueDate.ShouldBe(new DateTime(2026, 11, 1));
		});
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task A_Legacy_Hr_Zone_Should_Not_Be_Used_As_A_Confirmed_Library_Default(bool returning)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync(returning);
		await SetZoneAsync(LibraryPolicySettingNames.LegacyHrTimeZone, "Asia/Kolkata");

		var result = await ReadContextAsync(context, returning);

		result.SchoolToday.ShouldBeNull();
		result.ConfirmedTimeZoneId.ShouldBeNull();
		result.IsConfirmed.ShouldBeFalse();
		result.RequiresExplicitDate.ShouldBeTrue();
		result.UnavailableReason.ShouldNotBeNullOrWhiteSpace();
		result.AcademicYears.Select(row => row.Id).ShouldContain(context.Catalog.YearId);
	}

	[Fact]
	public async Task An_Invalid_Explicit_Library_Zone_Should_Keep_Manual_Date_Entry_Available_Without_A_False_Default()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync(false);
		await SetZoneAsync(LibraryPolicySettingNames.CalendarTimeZone, "Not/A-Real-School-Zone");

		var result = await ReadContextAsync(context, false);

		result.SchoolToday.ShouldBeNull();
		result.ConfirmedTimeZoneId.ShouldBeNull();
		result.IsConfirmed.ShouldBeFalse();
		result.RequiresExplicitDate.ShouldBeTrue();
		result.UnavailableReason.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task Owning_Lookup_Year_Options_Should_Be_Actual_Authorized_Tenant_Years_Without_Year_Admin_Grants()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync(false);
		LibraryCopyLifecycleTestContext foreign;
		using (LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider)) foreign = await SeedAsync(false);
		await WithUnitOfWorkAsync(async () =>
		{
			var actor = GetRequiredService<ICurrentUser>().Id!.Value;
			var assignments = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
			await assignments.DeleteAsync(row => row.UserId == actor, true);
			await assignments.InsertAsync(new(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id, userId: actor,
				academicYearId: context.Catalog.YearId, educationBoardId: context.BoardId, educationMediumId: context.MediumId,
				educationMediumStageId: context.StageId, standardId: context.BorrowerClassId), true);
			await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(actor);
		});

		var result = await ReadContextAsync(context, false);

		result.AcademicYears.Count.ShouldBe(1);
		result.AcademicYears.Single().Id.ShouldBe(context.Catalog.YearId);
		result.AcademicYears.Select(row => row.Id).ShouldNotContain(context.Catalog.OriginYearId);
		result.AcademicYears.Select(row => row.Id).ShouldNotContain(foreign.Catalog.YearId);
	}

	private async Task<LibraryCirculationSchoolContextDto> ReadContextAsync(LibraryCopyLifecycleTestContext context, bool returning)
	{
		var lookup = GetRequiredService<ILibraryCirculationLookupAppService>();
		var accession = await WithUnitOfWorkAsync(async () => (await GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(context.Catalog.CopyId)).AccessionNumber);
		if (!returning) return (await lookup.GetCopyAsync(accession)).SchoolContext;
		var result = await lookup.GetReturnAsync(accession);
		result.Loan.Id.ShouldBe(context.LoanId!.Value);
		result.Loan.Revision.ShouldNotBeNullOrWhiteSpace();
		return result.SchoolContext;
	}
	private Task SetZoneAsync(string name, string value) => WithUnitOfWorkAsync(() => GetRequiredService<ISettingManager>()
		.SetForTenantAsync(GetRequiredService<ICurrentTenant>().Id!.Value, name, value, forceToSet: true));
	private Task<LibraryCopyLifecycleTestContext> SeedAsync(bool returning)
	{
		var permissions = GetRequiredService<IPermissionChecker>();
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.Settings_BorrowingRules.Add).Returns(false);
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.View).Returns(false);
		return WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider, returning));
	}
}

[DependsOn(typeof(LibraryCanonicalLookupTestModule))]
public class LibraryCanonicalLookupCalendarTestModule : AbpModule
{
	public override void ConfigureServices(ServiceConfigurationContext context)
	{
		var clock = Substitute.For<IClock>();
		clock.Now.Returns(new DateTime(2026, 11, 17, 20, 30, 0, DateTimeKind.Utc));
		clock.Kind.Returns(DateTimeKind.Utc);
		clock.SupportsMultipleTimezone.Returns(true);
		clock.Normalize(Arg.Any<DateTime>()).Returns(call => call.Arg<DateTime>());
		context.Services.Replace(ServiceDescriptor.Singleton(clock));
	}
}

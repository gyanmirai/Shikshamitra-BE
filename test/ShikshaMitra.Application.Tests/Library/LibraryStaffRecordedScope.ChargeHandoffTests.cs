using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Charges;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryStaffRecordedScope_Tests
{
	[Fact]
	public async Task Library_Charge_Handoff_Detail_Should_Retain_The_Original_Recorded_Staff_Source_After_A_Move()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var (fixture, loan, source) = await SeedMovedStaffFinanceSourceAsync();
		await SetReaderStageAsync(fixture, fixture.Context.StageId);
		await AssertChargeHandoffScopeAsync(fixture, loan.Id, storedLoan: true, currentEmployee: false);
		_ = await ActualFinanceSourceReaderAsync(); // Real persisted identity and actual Charges.View grant/checker.

		var detail = await GetRequiredService<ILibraryChargeAppService>().GetAsync(source.SourceId);

		detail.Id.ShouldBe(source.SourceId);
		detail.MemberId.ShouldBe(fixture.EmployeeId);
		detail.Role.ShouldBe(1);
		detail.BookIssueId.ShouldBe(loan.Id);
		detail.AcademicYearId.ShouldBe(fixture.Context.Catalog.YearId);
		detail.SourceVersion.ShouldBe(source.SourceVersion);
		detail.SchoolChargeDate.ShouldBe(new DateOnly(2026, 11, 17));
		detail.SchoolDueDate.ShouldBe(new DateOnly(2026, 11, 30));
	}

	[Fact]
	public async Task Library_Charge_Handoff_Detail_Should_Deny_A_Current_Staff_Placement_That_Did_Not_Authorize_The_Original_Loan()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var (fixture, loan, source) = await SeedMovedStaffFinanceSourceAsync();
		await SetActualFinanceReaderStageAsync(fixture, fixture.LaterStageId);
		await AssertChargeHandoffScopeAsync(fixture, loan.Id, storedLoan: false, currentEmployee: true);
		_ = await ActualFinanceSourceReaderAsync();

		var error = await Should.ThrowAsync<BusinessException>(() =>
			GetRequiredService<ILibraryChargeAppService>().GetAsync(source.SourceId));

		error.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
	}

	[Fact]
	public async Task Library_Charge_Handoff_List_Should_Include_The_Recorded_Source_Before_Counting_And_Paging()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var (fixture, loan, source) = await SeedMovedStaffFinanceSourceAsync();
		await SetReaderStageAsync(fixture, fixture.Context.StageId);
		await AssertChargeHandoffScopeAsync(fixture, loan.Id, storedLoan: true, currentEmployee: false);
		_ = await ActualFinanceSourceReaderAsync();
		await AssertSingleChargeHandoffSourceAsync(source.SourceId);

		var service = GetRequiredService<ILibraryChargeAppService>();
		var first = await service.GetListAsync(page: 1, pageSize: 1);
		var second = await service.GetListAsync(page: 2, pageSize: 1);

		first.TotalCount.ShouldBe(1);
		first.Items.Single().Id.ShouldBe(source.SourceId);
		first.Items.Single().MemberId.ShouldBe(fixture.EmployeeId);
		first.Items.Single().AcademicYearId.ShouldBe(fixture.Context.Catalog.YearId);
		first.Items.Single().SourceVersion.ShouldBe(source.SourceVersion);
		second.TotalCount.ShouldBe(1);
		second.Items.ShouldBeEmpty();
	}

	[Fact]
	public async Task Library_Charge_Handoff_List_Should_Treat_An_Extreme_Page_As_Empty_Without_Integer_Overflow()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var (fixture, loan, source) = await SeedMovedStaffFinanceSourceAsync();
		await SetReaderStageAsync(fixture, fixture.Context.StageId);
		await AssertChargeHandoffScopeAsync(fixture, loan.Id, storedLoan: true, currentEmployee: false);
		_ = await ActualFinanceSourceReaderAsync();
		await AssertSingleChargeHandoffSourceAsync(source.SourceId);
		var service = GetRequiredService<ILibraryChargeAppService>();
		var first = await service.GetListAsync(page: 1, pageSize: 100);
		first.TotalCount.ShouldBe(1);
		first.Items.Single().Id.ShouldBe(source.SourceId);

		var extreme = await service.GetListAsync(page: int.MaxValue, pageSize: 100);

		extreme.TotalCount.ShouldBe(1);
		extreme.CurrentPage.ShouldBe(int.MaxValue);
		extreme.Items.ShouldBeEmpty();
	}

	[Fact]
	public async Task Library_Charge_Handoff_List_Should_Exclude_A_Current_Placement_Only_Source_From_Counts_And_Pages()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var (fixture, loan, source) = await SeedMovedStaffFinanceSourceAsync();
		await SetActualFinanceReaderStageAsync(fixture, fixture.LaterStageId);
		await AssertChargeHandoffScopeAsync(fixture, loan.Id, storedLoan: false, currentEmployee: true);
		_ = await ActualFinanceSourceReaderAsync();
		await AssertSingleChargeHandoffSourceAsync(source.SourceId);

		var service = GetRequiredService<ILibraryChargeAppService>();
		var first = await service.GetListAsync(page: 1, pageSize: 1);
		var second = await service.GetListAsync(page: 2, pageSize: 1);

		first.TotalCount.ShouldBe(0);
		second.TotalCount.ShouldBe(0);
		first.Items.ShouldBeEmpty();
		second.Items.ShouldBeEmpty();
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Library_Charge_Handoff_Direct_Assessment_Should_Continue_To_Use_Current_Staff_Scope(bool currentPlacement)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var (fixture, loan, linked) = await SeedMovedStaffFinanceSourceAsync();
		var directId = await WithUnitOfWorkAsync(async () =>
		{
			var charges = GetRequiredService<IRepository<LibraryCharge, Guid>>();
			var original = await charges.GetAsync(linked.SourceId);
			var actor = GetRequiredService<ICurrentUser>().Id!.Value;
			var direct = LibraryCharge.CreateConfirmed(Guid.NewGuid(), fixture.EmployeeId, 1, original.ChargeCategoryId,
				null, 75m, new(2026, 11, 17), new(2026, 11, 30), fixture.Context.Catalog.YearId,
				LibraryAcademicYearProvenance.ExplicitAssessmentContext,
				JsonSerializer.Serialize(new { AcademicYearId = fixture.Context.Catalog.YearId, ConfirmedBy = actor,
					Reason = "Explicit direct assessment under the current staff placement; no loan source." }),
				JsonSerializer.Serialize(new { SchoolChargeDate = "2026-11-17", SchoolDueDate = "2026-11-30",
					ConfirmedBy = actor, Reason = "Explicit direct school assessment dates." }),
				"Direct current-placement assessment", GetRequiredService<ICurrentTenant>().Id!.Value);
			await charges.InsertAsync(direct, true);
			return direct.Id;
		});
		if (currentPlacement) await SetActualFinanceReaderStageAsync(fixture, fixture.LaterStageId);
		else await SetReaderStageAsync(fixture, fixture.Context.StageId);
		await AssertChargeHandoffScopeAsync(fixture, loan.Id, storedLoan: !currentPlacement, currentEmployee: currentPlacement);
		_ = await ActualFinanceSourceReaderAsync();

		if (currentPlacement)
		{
			var detail = await GetRequiredService<ILibraryChargeAppService>().GetAsync(directId);
			detail.Id.ShouldBe(directId);
			detail.BookIssueId.ShouldBeNull();
			detail.MemberId.ShouldBe(fixture.EmployeeId);
			detail.AcademicYearId.ShouldBe(fixture.Context.Catalog.YearId);
		}
		else
		{
			var error = await Should.ThrowAsync<BusinessException>(() =>
				GetRequiredService<ILibraryChargeAppService>().GetAsync(directId));
			error.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		}
	}

	private async Task AssertChargeHandoffScopeAsync(StaffScopeFixture fixture, Guid loanId, bool storedLoan, bool currentEmployee)
	{
		var actorId = GetRequiredService<ICurrentUser>().Id!.Value;
		await WithUnitOfWorkAsync(async () =>
		{
			var identity = await GetRequiredService<IIdentityUserRepository>().GetAsync(actorId);
			identity.TenantId.ShouldBe(GetRequiredService<ICurrentTenant>().Id);
			identity.IsActive.ShouldBeTrue();
			var issue = await GetRequiredService<IRepository<BookIssue, Guid>>().GetAsync(loanId);
			issue.MemberId.ShouldBe(fixture.EmployeeId);
			issue.MemberRole.ShouldBe(1);
			issue.AcademicYearId.ShouldBe(fixture.Context.Catalog.YearId);
			issue.BorrowerScopeCaptureId.ShouldNotBeNull();
		});
		var scope = await GetRequiredService<IUserScopeAccessService>().GetCurrentUserScopeAsync();
		scope.UserId.ShouldBe(actorId);
		scope.TenantId.ShouldBe(GetRequiredService<ICurrentTenant>().Id);
		scope.HasTenantWideScope().ShouldBeFalse();
		(await WithUnitOfWorkAsync(() => GetRequiredService<LibraryStoredLoanScope>().CanReadAsync(loanId, scope)))
			.ShouldBe(storedLoan);
		(await WithUnitOfWorkAsync(async () => await GetRequiredService<IAsyncQueryableExecuter>().AnyAsync(
			(await GetRequiredService<ILibraryAccessService>().GetEmployeesAsync()).Where(row => row.Id == fixture.EmployeeId))))
			.ShouldBe(currentEmployee);
	}

	private Task AssertSingleChargeHandoffSourceAsync(Guid sourceId) => WithUnitOfWorkAsync(async () =>
	{
		var charges = GetRequiredService<IRepository<LibraryCharge, Guid>>();
		(await charges.CountAsync()).ShouldBe(1, "This isolated school has one exact loan-linked assessment.");
		(await charges.GetAsync(sourceId)).BookIssueId.ShouldNotBeNull();
	});
}


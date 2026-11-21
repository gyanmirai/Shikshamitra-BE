using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Charges;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryStaffRecordedScope_Tests
{
	[Fact]
	public async Task Library_Charge_Year_Read_Should_Deny_A_Known_Assessment_Year_Outside_The_Readers_Allowed_Years()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await PrepareChargeYearBoundaryAsync();

		var denied = await Should.ThrowAsync<BusinessException>(() =>
			GetRequiredService<ILibraryChargeAppService>().GetAsync(context.Later.SourceId));

		denied.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
	}

	[Fact]
	public async Task Library_Charge_Year_Read_Should_Exclude_An_Unauthorized_Assessment_Year_Before_Counts_And_Pages()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await PrepareChargeYearBoundaryAsync();
		var service = GetRequiredService<ILibraryChargeAppService>();

		var first = await service.GetListAsync(page: 1, pageSize: 1);
		var second = await service.GetListAsync(page: 2, pageSize: 1);

		first.TotalCount.ShouldBe(1);
		first.Items.Single().Id.ShouldBe(context.Original.SourceId);
		second.TotalCount.ShouldBe(1);
		second.Items.ShouldBeEmpty();
	}

	[Fact]
	public async Task Library_Charge_Year_Read_Should_Allow_Distinct_Loan_And_Assessment_Years_When_Both_Are_Authorized()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await PrepareChargeYearBoundaryAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			var userId = GetRequiredService<ICurrentUser>().Id!.Value;
			await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(
				Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id, userId: userId,
				academicYearId: context.LaterYearId, description: "Independent actual assessment-year authority"), true);
			await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(userId);
		});
		await GetRequiredService<ILibraryAccessService>().GetContextAsync(context.LaterYearId);
		await AssertChargeHandoffScopeAsync(context.Fixture, context.LoanId, storedLoan: true, currentEmployee: false);

		var service = GetRequiredService<ILibraryChargeAppService>();
		var detail = await service.GetAsync(context.Later.SourceId);
		var page = await service.GetListAsync(page: 1, pageSize: 100);

		detail.BookIssueId.ShouldBe(context.LoanId);
		detail.AcademicYearId.ShouldBe(context.LaterYearId);
		context.LaterYearId.ShouldNotBe(context.Fixture.Context.Catalog.YearId);
		page.TotalCount.ShouldBe(2);
		page.Items.Select(item => item.Id).OrderBy(id => id)
			.ShouldBe(new[] { context.Original.SourceId, context.Later.SourceId }.OrderBy(id => id));
	}

	[Fact]
	public async Task Library_Charge_Year_Read_Should_Keep_A_Legacy_Unknown_Year_Visible_Through_Its_Exact_Authorized_Source()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var (fixture, loan, original) = await SeedMovedStaffFinanceSourceAsync();
		var legacyId = await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<LibraryCharge, Guid>>();
			var source = await repository.GetAsync(original.SourceId);
			var legacy = new LibraryCharge(Guid.NewGuid(), fixture.EmployeeId, 1, source.ChargeCategoryId, loan.Id,
				20m, new DateTime(2026, 11, 17, 0, 0, 0, DateTimeKind.Utc),
				new DateTime(2026, 11, 30, 0, 0, 0, DateTimeKind.Utc), GetRequiredService<ICurrentTenant>().Id);
			await repository.InsertAsync(legacy, true);
			return legacy.Id;
		});
		await SetReaderStageAsync(fixture, fixture.Context.StageId);
		_ = await ActualFinanceSourceReaderAsync();
		await AssertChargeHandoffScopeAsync(fixture, loan.Id, storedLoan: true, currentEmployee: false);

		var service = GetRequiredService<ILibraryChargeAppService>();
		var detail = await service.GetAsync(legacyId);
		var page = await service.GetListAsync(page: 1, pageSize: 100);

		detail.AcademicYearId.ShouldBeNull();
		detail.AcademicYearProvenance.ShouldBe(LibraryAcademicYearProvenance.Unconfirmed);
		detail.SchoolChargeDate.ShouldBeNull();
		detail.SchoolDueDate.ShouldBeNull();
		detail.ChargeDate.ShouldBe(new DateTime(2026, 11, 17, 0, 0, 0, DateTimeKind.Utc));
		detail.DueDate.ShouldBe(new DateTime(2026, 11, 30, 0, 0, 0, DateTimeKind.Utc));
		detail.BookIssueId.ShouldBe(loan.Id);
		detail.FinanceSourceLinkId.ShouldBeNull();
		page.TotalCount.ShouldBe(2);
		page.Items.ShouldContain(item => item.Id == legacyId && item.AcademicYearId == null);
	}

	private async Task<(StaffScopeFixture Fixture, Guid LoanId, FinanceExternalSourceReference Original,
		FinanceExternalSourceReference Later, Guid LaterYearId)> PrepareChargeYearBoundaryAsync()
	{
		var (fixture, loan, original) = await SeedMovedStaffFinanceSourceAsync();
		var (later, laterYearId) = await NewStaffAssessmentYearSourceAsync(fixture, loan.Id);
		await SetReaderStageAsync(fixture, fixture.Context.StageId);
		_ = await ActualFinanceSourceReaderAsync();
		await AssertChargeHandoffScopeAsync(fixture, loan.Id, storedLoan: true, currentEmployee: false);
		var error = await Should.ThrowAsync<BusinessException>(() =>
			GetRequiredService<ILibraryAccessService>().GetContextAsync(laterYearId));
		error.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<LibraryCharge, Guid>>();
			(await repository.CountAsync()).ShouldBe(2);
			var charge = await repository.GetAsync(later.SourceId);
			charge.BookIssueId.ShouldBe(loan.Id);
			charge.MemberId.ShouldBe(fixture.EmployeeId);
			charge.MemberRole.ShouldBe(1);
			charge.AcademicYearId.ShouldBe(laterYearId);
			charge.AcademicYearId.ShouldNotBe(loan.AcademicYearId);
		});
		return (fixture, loan.Id, original, later, laterYearId);
	}
}

using System;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Setting.ChargeCategory;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryStaffRecordedScope_Tests
{
	[Fact]
	public async Task A_Staff_Assessment_Year_Should_Remain_Separate_From_The_Recorded_Loan_Year()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var (fixture, loan, _) = await SeedMovedStaffFinanceSourceAsync();
		var (source, assessmentYearId) = await NewStaffAssessmentYearSourceAsync(fixture, loan.Id);
		await SetReaderStageAsync(fixture, fixture.Context.StageId);
		var scope = await GetRequiredService<IUserScopeAccessService>().GetCurrentUserScopeAsync();
		(await WithUnitOfWorkAsync(() => GetRequiredService<LibraryStoredLoanScope>().CanReadAsync(loan.Id, scope))).ShouldBeTrue();
		var reader = await ActualFinanceSourceReaderAsync();

		var facts = await WithUnitOfWorkAsync(() => reader.ReadAuthorizedAsync(source));

		facts.AcademicYearId.ShouldBe(assessmentYearId);
		facts.Payer.EmployeeId.ShouldBe(fixture.EmployeeId);
		(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<BookIssue, Guid>>().GetAsync(loan.Id)))
			.AcademicYearId.ShouldBe(fixture.Context.Catalog.YearId);
		await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(() =>
			GetRequiredService<FinanceScopeAccessService>().EnsureCollectionYearAccessibleAsync(assessmentYearId)));
	}

	[Fact]
	public async Task A_Reviewed_Staff_Assessment_Should_Not_Invent_An_Unknown_Original_Loan_Year()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		var loanId = await WithUnitOfWorkAsync(async () =>
		{
			var copy = await GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(fixture.Context.Catalog.CopyId);
			var loan = new BookIssue(Guid.NewGuid(), copy.Id, copy.AccessionNumber, fixture.EmployeeId, 1,
				new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
				new DateTime(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc), GetRequiredService<ICurrentTenant>().Id);
			await GetRequiredService<IRepository<BookIssue, Guid>>().InsertAsync(loan, true);
			return loan.Id;
		});
		var (source, assessmentYearId) = await NewStaffAssessmentYearSourceAsync(fixture, loanId);
		var reader = await ActualFinanceSourceReaderAsync();
		(await GetRequiredService<IUserScopeAccessService>().GetCurrentUserScopeAsync()).HasTenantWideScope().ShouldBeTrue();

		var facts = await WithUnitOfWorkAsync(() => reader.ReadAuthorizedAsync(source));

		facts.AcademicYearId.ShouldBe(assessmentYearId);
		facts.AcademicYearConfirmed.ShouldBeTrue();
		await WithUnitOfWorkAsync(async () =>
		{
			var loan = await GetRequiredService<IRepository<BookIssue, Guid>>().GetAsync(loanId);
			loan.AcademicYearId.ShouldBeNull();
			loan.BorrowerScopeCaptureId.ShouldBeNull();
			loan.SchoolIssueDate.ShouldBeNull();
			loan.SchoolDueDate.ShouldBeNull();
		});
	}

	private Task<(FinanceExternalSourceReference Source, Guid YearId)> NewStaffAssessmentYearSourceAsync(
		StaffScopeFixture fixture, Guid loanId) => WithUnitOfWorkAsync(async () =>
	{
		var tenant = GetRequiredService<ICurrentTenant>().Id!.Value;
		var year = new AcademicYear(Guid.NewGuid(), tenant, "Later assessment year",
			new DateTime(2027, 4, 1), new DateTime(2028, 3, 31));
		await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(year, true);
		var category = new LibraryChargeCategory(Guid.NewGuid(), tenant, year.Id, "New recovery assessment", 75m, false,
			"Independent current assessment year");
		await GetRequiredService<IRepository<LibraryChargeCategory, Guid>>().InsertAsync(category, true);
		var actor = GetRequiredService<ICurrentUser>().Id!.Value;
		var charge = LibraryCharge.CreateConfirmed(Guid.NewGuid(), fixture.EmployeeId, 1, category.Id, loanId,
			75m, new(2027, 11, 17), new(2027, 11, 30), year.Id, LibraryAcademicYearProvenance.ExplicitAssessmentContext,
			JsonSerializer.Serialize(new { LoanId = loanId, AssessmentYearId = year.Id, ActorId = actor,
				Reason = "New recovery assessment; the original loan year is retained separately." }),
			JsonSerializer.Serialize(new { ActorId = actor, Reason = "Explicit new assessment school dates." }),
			"New recovery assessment under the selected year.", tenant);
		await GetRequiredService<IRepository<LibraryCharge, Guid>>().InsertAsync(charge, true);
		return (new FinanceExternalSourceReference
		{
			Kind = FinanceExternalSourceKind.LibraryAssessment, SourceId = charge.Id, SourceVersion = charge.GetAssessmentVersion()
		}, year.Id);
	});
}

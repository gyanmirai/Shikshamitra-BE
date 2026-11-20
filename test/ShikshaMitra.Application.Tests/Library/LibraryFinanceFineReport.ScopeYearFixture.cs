using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Circulation;
using ShikshaMitra.Library.Setting.ChargeCategory;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	private Task<FineReportSavedYear> SeedFineReportIndependentYearAsync(MovedTypedCashFixture moved) => WithUnitOfWorkAsync(async () =>
	{
		var owner = moved.Settlement.Owner;
		var loan = await GetRequiredService<IRepository<BookIssue, Guid>>().GetAsync(moved.Source.LoanId);
		loan.AcademicYearId.ShouldBe(owner.YearId);
		var snapshot = LibraryLoanBorrowerSnapshot.Read(loan, owner.PersonId);
		snapshot.ShouldNotBeNull();
		snapshot!.MemberName.ShouldNotBeNullOrWhiteSpace();
		var year = new AcademicYear(Guid.NewGuid(), owner.TenantId, "Independent report assessment year",
			new DateTime(2027, 4, 1), new DateTime(2028, 3, 31));
		await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(year, true);
		var category = new LibraryChargeCategory(Guid.NewGuid(), owner.TenantId, year.Id,
			"Later report recovery", 75m, false, "Assessment year is independent of the original loan year.");
		await GetRequiredService<IRepository<LibraryChargeCategory, Guid>>().InsertAsync(category, true);
		var id = Guid.NewGuid();
		var charge = LibraryCharge.CreateConfirmed(id, owner.PersonId, 1, category.Id, loan.Id, 75m,
			new(2027, 11, 17), new(2027, 11, 30), year.Id, LibraryAcademicYearProvenance.ExplicitAssessmentContext,
			JsonSerializer.Serialize(new { AssessmentId = id, LoanId = loan.Id, AssessmentYearId = year.Id,
				ActorId = owner.ActorId, Reason = "Explicit later assessment; preserve the original loan year." }),
			JsonSerializer.Serialize(new { AssessmentId = id, ActorId = owner.ActorId, Reason = "Explicit school assessment dates." }),
			"Independent later-year recovery assessment.", owner.TenantId);
		charge.ChargeDate.ShouldBeNull(); charge.SchoolChargeDate.ShouldBe(new DateOnly(2027, 11, 17));
		var charges = GetRequiredService<IRepository<LibraryCharge, Guid>>();
		await charges.InsertAsync(charge, true);
		var original = await charges.GetAsync(owner.AssessmentId);
		var unknown = new LibraryCharge(Guid.NewGuid(), owner.PersonId, 1, original.ChargeCategoryId, loan.Id,
			45.25m, new DateTime(2024, 6, 10), new DateTime(2024, 6, 24), owner.TenantId);
		unknown.AcademicYearId.ShouldBeNull(); unknown.Currency.ShouldBeNull();
		await charges.InsertAsync(unknown, true);
		return new FineReportSavedYear(year.Id, charge.Id, unknown.Id, snapshot.MemberName!);
	});

	private Task GrantFineReportIndependentYearAsync(Fixture owner, Guid yearId) => WithUnitOfWorkAsync(async () =>
	{
		await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(Guid.NewGuid(),
			owner.TenantId, userId: owner.ActorId, academicYearId: yearId,
			description: "Independent permission for the saved billing year, preserving original loan scope."), true);
		await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(owner.ActorId);
	});

	private Task AssertFineReportMovedScopeAsync(MovedTypedCashFixture moved, bool currentPlacement) => WithUnitOfWorkAsync(async () =>
	{
		var owner = moved.Settlement.Owner;
		var context = await GetRequiredService<ILibraryAccessService>().GetContextAsync(owner.YearId);
		context.UserScope.HasTenantWideScope().ShouldBeFalse();
		(await GetRequiredService<LibraryStoredLoanScope>().CanReadAsync(moved.Source.LoanId, context.UserScope)).ShouldBe(!currentPlacement);
		var employees = await GetRequiredService<ILibraryAccessService>().GetEmployeesAsync(context.UserScope);
		(await GetRequiredService<IAsyncQueryableExecuter>().AnyAsync(employees.Where(row => row.Id == owner.PersonId))).ShouldBe(currentPlacement);
	});

	private sealed record FineReportSavedYear(Guid YearId, Guid AssessmentId, Guid UnknownYearAssessmentId, string RecordedName);
}

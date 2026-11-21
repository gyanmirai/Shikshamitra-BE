using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Library.Shared;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryLoanMaintenanceRead_Tests
{
	[Fact]
	public async Task Loan_Maintenance_Read_Should_Provide_Confirmed_Context_With_View_And_Renew_Only()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedLegacyLoanAsync();
		await SetZoneAsync(LibraryPolicySettingNames.CalendarTimeZone, "Asia/Kolkata");
		await OwnOnlyAsync(View, Renew);
		await AssertLegacyDatesUntouchedAsync(fixture.LoanId!.Value);

		var loan = await Loans.GetAsync(fixture.LoanId.Value);

		loan.Id.ShouldBe(fixture.LoanId.Value); loan.CopyId.ShouldBe(fixture.Catalog.CopyId);
		loan.CanReturn.ShouldBeFalse();
		loan.IssueDateProvenance.ShouldBe(0); loan.DueDateProvenance.ShouldBe(0);
		loan.RawIssueDate.ShouldBe(new DateTime(2026, 11, 1));
		var context = ContextOf(loan);
		context.SchoolToday.ShouldBe(new DateOnly(2026, 11, 18)); // Fixed UTC clock is still November 17.
		context.ConfirmedTimeZoneId.ShouldBe("Asia/Kolkata");
		context.IsConfirmed.ShouldBeTrue(); context.RequiresExplicitDate.ShouldBeFalse();
		context.UnavailableReason.ShouldBeNull();
		context.AcademicYears.Select(row => row.Id).ShouldContain(fixture.Catalog.YearId);
		context.AcademicYears.ShouldAllBe(row => !string.IsNullOrWhiteSpace(row.Name));
		await AssertLegacyDatesUntouchedAsync(loan.Id);
	}

	[Theory]
	[InlineData("Missing")]
	[InlineData("LegacyHr")]
	[InlineData("InvalidLibrary")]
	public async Task Loan_Maintenance_Read_Should_Preserve_An_Unknown_School_Day_Without_A_Fallback(string configuration)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedLegacyLoanAsync();
		if (configuration == "LegacyHr") await SetZoneAsync(LibraryPolicySettingNames.LegacyHrTimeZone, "Asia/Kolkata");
		if (configuration == "InvalidLibrary") await SetZoneAsync(LibraryPolicySettingNames.CalendarTimeZone, "Not/A-School-Zone");
		await OwnOnlyAsync(View, Renew);
		await AssertLegacyDatesUntouchedAsync(fixture.LoanId!.Value);

		var context = ContextOf(await Loans.GetAsync(fixture.LoanId.Value));

		context.SchoolToday.ShouldBeNull(); context.ConfirmedTimeZoneId.ShouldBeNull();
		context.IsConfirmed.ShouldBeFalse(); context.RequiresExplicitDate.ShouldBeTrue();
		context.UnavailableReason.ShouldNotBeNullOrWhiteSpace();
		context.AcademicYears.Select(row => row.Id).ShouldContain(fixture.Catalog.YearId);
		await AssertLegacyDatesUntouchedAsync(fixture.LoanId.Value);
	}

	[Fact]
	public async Task Loan_Maintenance_Read_Should_Offer_Actual_Authorized_Years_Independently_Of_The_Loan_Year()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await WithUnitOfWorkAsync<LibraryCopyLifecycleTestContext>(() =>
			LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider));
		var loanId = await SeedCivilLoanAsync(fixture, legacyReturned: true);
		await AssertCivilReviewShapeAsync(loanId, legacyReturned: true);
		var hiddenYear = await AddYearAsync("Outside allowed year", 2027);
		Guid foreignYear;
		using (LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider))
			foreignYear = (await SeedLegacyLoanAsync()).Catalog.YearId;
		await RestrictToRecordedClassAsync(fixture, includeOtherYear: true);
		await OwnOnlyAsync(View, Review);
		var access = await WithUnitOfWorkAsync<LibraryAcademicContext>(() =>
			GetRequiredService<ILibraryAccessService>().GetContextAsync());
		access.UserScope.HasTenantWideScope().ShouldBeFalse();
		access.AllowedAcademicYearIds.OrderBy(id => id).ShouldBe(
			new[] { fixture.Catalog.YearId, fixture.Catalog.OriginYearId }.OrderBy(id => id));
		(await WithUnitOfWorkAsync<bool>(() => GetRequiredService<LibraryStoredLoanScope>().CanReadAsync(loanId, access.UserScope)))
			.ShouldBeTrue();

		var loan = await Loans.GetAsync(loanId);
		var context = ContextOf(loan);

		loan.AcademicYearId.ShouldBe(fixture.Catalog.YearId);
		context.AcademicYears.Select(row => row.Id).OrderBy(id => id).ShouldBe(
			new[] { fixture.Catalog.YearId, fixture.Catalog.OriginYearId }.OrderBy(id => id));
		context.AcademicYears.Select(row => row.Id).ShouldNotContain(hiddenYear);
		context.AcademicYears.Select(row => row.Id).ShouldNotContain(foreignYear);
		context.AcademicYears.ShouldAllBe(row => !string.IsNullOrWhiteSpace(row.Name));
		CanReviewDatesOf(loan).ShouldBeTrue("Date review uses recorded row scope, not whole-school settings authority.");
		await AssertCivilReviewShapeAsync(loanId, legacyReturned: true);
	}
}

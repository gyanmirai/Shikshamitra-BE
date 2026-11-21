using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryLoanMaintenanceRead_Tests
{
	[Fact]
	public async Task Loan_Maintenance_Read_Should_Deny_Another_Recorded_Class_Before_Context_Or_Capability()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await WithUnitOfWorkAsync<LibraryCopyLifecycleTestContext>(() =>
			LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider));
		var loanId = await SeedCivilLoanAsync(fixture, otherBorrower: true);
		await RestrictToRecordedClassAsync(fixture);
		await OwnOnlyAsync(View, Renew, Review);
		var scope = (await WithUnitOfWorkAsync<LibraryAcademicContext>(() =>
			GetRequiredService<ILibraryAccessService>().GetContextAsync())).UserScope;
		scope.HasTenantWideScope().ShouldBeFalse();
		(await WithUnitOfWorkAsync<bool>(() => GetRequiredService<LibraryStoredLoanScope>().CanReadAsync(loanId, scope)))
			.ShouldBeFalse();

		var error = await Should.ThrowAsync<BusinessException>(() => Loans.GetAsync(loanId));

		error.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
	}

	[Fact]
	public async Task Loan_Maintenance_Read_Should_Deny_A_Foreign_Loan_With_The_Same_Owning_Actions()
	{
		using var original = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var source = await SeedLegacyLoanAsync();
		await OwnOnlyAsync(View, Renew, Review);
		(await Loans.GetAsync(source.LoanId!.Value)).Id.ShouldBe(source.LoanId.Value);
		using var otherSchool = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var other = await SeedLegacyLoanAsync();
		other.LoanId.ShouldNotBe(source.LoanId);
		await OwnOnlyAsync(View, Renew, Review);
		(await Loans.GetAsync(other.LoanId!.Value)).Id.ShouldBe(other.LoanId.Value);

		var error = await Should.ThrowAsync<BusinessException>(() => Loans.GetAsync(source.LoanId.Value));

		error.Code.ShouldBe(LibraryCirculationErrorCodes.LoanNotFound);
	}
}

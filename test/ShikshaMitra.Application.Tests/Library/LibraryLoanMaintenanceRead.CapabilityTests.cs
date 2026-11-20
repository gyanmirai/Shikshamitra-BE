using System;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryLoanMaintenanceRead_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Loan_Maintenance_Read_Should_Report_Only_The_Current_Own_Date_Review_Grant(bool canReview)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedLegacyLoanAsync();
		await OwnOnlyAsync(View, canReview ? Review : Renew);
		(await Permissions.IsGrantedAsync(Review)).ShouldBe(canReview);
		(await Permissions.IsGrantedAsync(Renew)).ShouldBe(!canReview);

		var loan = await Loans.GetAsync(fixture.LoanId!.Value);

		loan.Id.ShouldBe(fixture.LoanId.Value);
		CanReviewDatesOf(loan).ShouldBe(canReview);
		await AssertLegacyDatesUntouchedAsync(loan.Id);
	}

	[Fact]
	public async Task Loan_Maintenance_Read_Should_Not_Offer_Date_Review_When_An_Issued_Civil_Loan_Has_No_Reviewable_Fields()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await WithUnitOfWorkAsync<LibraryCopyLifecycleTestContext>(() =>
			LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider));
		var loanId = await SeedCivilLoanAsync(fixture);
		await OwnOnlyAsync(View, Review);
		(await Permissions.IsGrantedAsync(Review)).ShouldBeTrue();
		await AssertCivilReviewShapeAsync(loanId, legacyReturned: false);

		var loan = await Loans.GetAsync(loanId);

		loan.Id.ShouldBe(loanId);
		CanReviewDatesOf(loan).ShouldBeFalse("The grant cannot make confirmed issue/due/original dates reviewable, and an issued loan has no applicable return field.");
		await AssertCivilReviewShapeAsync(loanId, legacyReturned: false);
	}

	[Theory]
	[InlineData(Renew)]
	[InlineData(Review)]
	public async Task Loan_Maintenance_Read_Should_Reject_Action_Only_Without_View_Before_Returning_Context(string action)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedLegacyLoanAsync();
		await OwnOnlyAsync(action);
		(await Permissions.IsGrantedAsync(View)).ShouldBeFalse();

		await Should.ThrowAsync<AbpAuthorizationException>(() => Loans.GetAsync(fixture.LoanId!.Value));
	}

	[Fact]
	public async Task Loan_Maintenance_Read_Should_Reject_A_Disabled_Persisted_Actor_Despite_Still_Granted_Actions()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedLegacyLoanAsync();
		await OwnOnlyAsync(View, Renew, Review);
		(await Loans.GetAsync(fixture.LoanId!.Value)).Id.ShouldBe(fixture.LoanId.Value);
		var actorId = GetRequiredService<ICurrentUser>().Id!.Value;
		await WithUnitOfWorkAsync(async () =>
		{
			var identities = GetRequiredService<IIdentityUserRepository>();
			var actor = await identities.GetAsync(actorId);
			actor.IsActive.ShouldBeTrue(); actor.SetIsActive(false);
			await identities.UpdateAsync(actor, true);
		});
		await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(actorId);
		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<IIdentityUserRepository>().GetAsync(actorId)).IsActive.ShouldBeFalse();
			(await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().CountAsync(row => row.UserId == actorId && row.IsActive))
				.ShouldBeGreaterThan(0);
		});
		(await Permissions.IsGrantedAsync(View)).ShouldBeTrue();
		(await Permissions.IsGrantedAsync(Review)).ShouldBeTrue();

		var error = await Should.ThrowAsync<BusinessException>(() => Loans.GetAsync(fixture.LoanId!.Value));

		error.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
	}
}

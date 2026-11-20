using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryIssuePlan_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task New_Loan_Preview_Should_Use_The_Borrowers_Current_Year_Instead_Of_The_Stock_Origin_Year()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();

		var result = await Plans.PreviewAsync(Request(context));

		result.AcademicYearId.ShouldBe(context.Catalog.YearId);
		result.AcademicYearId.ShouldNotBe(context.Catalog.OriginYearId);
		result.Policy.Terms.MaxBooksAtTime.ShouldBe(4);
		result.CopyId.ShouldBe(context.Catalog.CopyId);
		result.MemberId.ShouldBe(context.StudentId);
		result.Revision.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task Manual_School_Dates_Should_Remain_Exact_Without_An_Inferred_Timezone()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var input = Request(context);

		var result = await Plans.PreviewAsync(input);

		result.Policy.IsLegacyManualDate.ShouldBeTrue();
		result.IssueDate.ShouldBe(input.IssueDate);
		result.DueDate.ShouldBe(input.RequestedDueDate);
		result.ConfirmedSchoolTimeZoneId.ShouldBeNull();
		result.CanIssue.ShouldBeTrue();
	}

	[Fact]
	public async Task Published_Policy_Should_Calculate_Due_Date_And_Reject_An_Unapproved_Manual_Override()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		await GetRequiredService<ILibraryCalendarAppService>().UpdateSettingsAsync(new() { TimeZoneId = "Asia/Kolkata" });
		var policy = await PublishAsync(context.Catalog.YearId);
		var input = Request(context);
		input.RequestedDueDate = null;

		var result = await Plans.PreviewAsync(input);

		result.Policy.PolicyVersionId.ShouldBe(policy.Id);
		result.DueDate.ShouldBe(new DateOnly(2026, 11, 24));
		result.ConfirmedSchoolTimeZoneId.ShouldBe("Asia/Kolkata");
		input.RequestedDueDate = new(2026, 11, 30);
		(await Should.ThrowAsync<BusinessException>(() => Plans.PreviewAsync(input))).Code.ShouldBe(LibraryErrorCodes.PolicyInvalid);
	}

	[Fact]
	public async Task A_Physical_Copy_With_An_Open_Loan_Should_Not_Be_Offered_For_Another_Checkout()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync(onLoan: true);

		var result = await Plans.PreviewAsync(Request(context));

		result.HasOpenLoan.ShouldBeTrue();
		result.CanIssue.ShouldBeFalse();
		result.BlockedReasons.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task Preview_Should_Require_Actual_User_Scope_Before_Returning_A_Plan()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await SeedAsync();
		var userId = GetRequiredService<ICurrentUser>().Id!.Value;
		await WithUnitOfWorkAsync(async () =>
		{
			await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().DeleteAsync(row => row.UserId == userId, true);
			await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(userId);
		});

		(await Should.ThrowAsync<BusinessException>(() => Plans.PreviewAsync(Request(context))))
			.Code.ShouldBe(UserScopeErrorCodes.AccessDenied);
	}

	private ILibraryIssuePlanAppService Plans => GetRequiredService<ILibraryIssuePlanAppService>();
	private Task<LibraryCopyLifecycleTestContext> SeedAsync(bool onLoan = false) =>
		WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider, onLoan));
	private static LibraryIssuePlanRequest Request(LibraryCopyLifecycleTestContext context) => new()
	{
		CopyId = context.Catalog.CopyId, MemberId = context.StudentId,
		IssueDate = new(2026, 11, 17), RequestedDueDate = new(2026, 11, 30)
	};
	private async Task<LibraryBorrowingPolicyVersionDto> PublishAsync(Guid yearId)
	{
		var policies = GetRequiredService<ILibraryBorrowingPolicyAppService>();
		var draft = await policies.CreateAsync(yearId, new()
		{
			Name = "Current student borrowing", Role = 0, EffectiveFrom = new(2026, 11, 1),
			Terms = new() { MaxBooksAtTime = 2, ReservationLimit = 2, LoanDays = 7, MaxRenewals = 1, RenewalDays = 7, PickupWindowDays = 2 }
		});
		return await policies.PublishAsync(draft.Id, new() { ConcurrencyStamp = draft.ConcurrencyStamp! });
	}
}

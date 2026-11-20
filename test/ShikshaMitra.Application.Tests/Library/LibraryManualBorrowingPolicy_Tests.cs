using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Policies;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryManualBorrowingPolicy_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Fact]
	public async Task A_Manual_Due_Date_Policy_Should_Save_Renewal_Limits_Without_Forcing_Automated_Calendar_Setup()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider));
		var policies = GetRequiredService<ILibraryBorrowingPolicyAppService>();
		var draft = await policies.CreateAsync(context.Catalog.YearId, new()
		{
			Name = "Manual school borrowing", Role = 0, EffectiveFrom = new(2026, 11, 1),
			Terms = new()
			{
				DueDateMode = 0, MaxBooksAtTime = 2, ReservationLimit = 1, MaxRenewals = 1,
				LoanDays = 0, RenewalDays = 0, PickupWindowDays = 0
			}
		});
		var published = await policies.PublishAsync(draft.Id, new() { ConcurrencyStamp = draft.ConcurrencyStamp! });
		var plan = await GetRequiredService<ILibraryIssuePlanAppService>().PreviewAsync(new()
		{
			CopyId = context.Catalog.CopyId, MemberId = context.StudentId, IssueDate = new(2026, 11, 17),
			RequestedDueDate = new(2026, 11, 30)
		});
		plan.Policy.PolicyVersionId.ShouldBe(published.Id);
		plan.Policy.IsLegacyManualDate.ShouldBeFalse();
		plan.Policy.Terms.DueDateMode.ShouldBe(0);
		plan.Policy.Terms.MaxRenewals.ShouldBe(1);
		plan.Policy.Terms.LoanDays.ShouldBeNull();
		plan.Policy.Terms.RenewalDays.ShouldBeNull();
		plan.Policy.Terms.PickupWindowDays.ShouldBeNull();
		plan.DueDate.ShouldBe(new DateOnly(2026, 11, 30));
		plan.ConfirmedSchoolTimeZoneId.ShouldBeNull();
	}
}

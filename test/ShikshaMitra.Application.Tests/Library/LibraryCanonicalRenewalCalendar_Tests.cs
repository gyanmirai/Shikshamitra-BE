using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Policies;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryCanonicalRenewal_Tests
{
	[Fact]
	public async Task A_New_Renewal_Calendar_Should_Require_Explicit_Review_And_Preserve_The_Original_Policy_Zone()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider));
		var calendar = GetRequiredService<ILibraryCalendarAppService>();
		await calendar.UpdateSettingsAsync(new() { TimeZoneId = "Asia/Kolkata" });
		await PublishAsync(context.Catalog.YearId, 7, 1);
		var input = new LibraryCheckoutRequest
		{
			CopyId = context.Catalog.CopyId, MemberId = context.StudentId, IssueDate = new(2026, 11, 17),
			IdempotencyKey = Guid.NewGuid().ToString("N")
		};
		input.Revision = (await GetRequiredService<ILibraryIssuePlanAppService>().PreviewAsync(new()
		{
			CopyId = input.CopyId, MemberId = input.MemberId, IssueDate = input.IssueDate
		})).Revision;
		var loans = GetRequiredService<ILibraryLoanAppService>();
		var issued = await loans.CheckoutAsync(input);
		await calendar.UpdateSettingsAsync(new() { TimeZoneId = "UTC" });
		var plan = await loans.PreviewRenewalAsync(issued.Loan.Id, new() { SchoolRenewalDate = new(2026, 11, 17) });
		plan.RequiresCalendarChangeAcknowledgment.ShouldBeTrue();
		plan.CurrentSchoolTimeZoneId.ShouldBe("UTC");
		var renew = new LibraryLoanRenewRequest
		{
			Revision = plan.Revision, IdempotencyKey = Guid.NewGuid().ToString("N"), SchoolRenewalDate = new(2026, 11, 17)
		};
		(await Should.ThrowAsync<BusinessException>(() => loans.RenewAsync(issued.Loan.Id, renew))).Code
			.ShouldBe(LibraryCirculationErrorCodes.CalendarChangeRequiresReview);
		(await loans.GetAsync(issued.Loan.Id)).RenewalCount.ShouldBe(0);
		renew.IdempotencyKey = Guid.NewGuid().ToString("N");
		renew.CalendarChangeReason = "Reviewed the school's confirmed calendar change before calculating this renewal.";
		var result = await loans.RenewAsync(issued.Loan.Id, renew);
		result.Loan.Policy!.TimeZoneId.ShouldBe("Asia/Kolkata");
		result.Loan.OriginalSchoolDueDate.ShouldBe(new DateOnly(2026, 11, 24));
		result.Loan.SchoolDueDate.ShouldBe(new DateOnly(2026, 12, 1));
	}
}

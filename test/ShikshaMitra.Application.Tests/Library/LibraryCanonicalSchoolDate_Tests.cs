using System;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Policies;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryCanonicalRenewal_Tests
{
	[Theory]
	[InlineData(6, false)]
	[InlineData(20, true)]
	public async Task Automated_Checkout_Should_Use_The_Confirmed_School_Day_To_Reject_Future_Issue_Dates(int utcHour, bool schoolDayReached)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider));
		GetRequiredService<IClock>().Now.Returns(new DateTime(2026, 11, 17, utcHour, 0, 0, DateTimeKind.Utc));
		await GetRequiredService<ILibraryCalendarAppService>().UpdateSettingsAsync(new() { TimeZoneId = "Asia/Kolkata" });
		await PublishAsync(context.Catalog.YearId, 7, 1);
		var input = new LibraryIssuePlanRequest
		{
			CopyId = context.Catalog.CopyId, MemberId = context.StudentId, IssueDate = new(2026, 11, 18)
		};
		var plans = GetRequiredService<ILibraryIssuePlanAppService>();
		var loans = GetRequiredService<ILibraryLoanAppService>();
		if (schoolDayReached)
		{
			var plan = await plans.PreviewAsync(input);
			plan.CanIssue.ShouldBeTrue();
			var result = await loans.CheckoutAsync(new()
			{
				CopyId = input.CopyId, MemberId = input.MemberId, IssueDate = input.IssueDate,
				Revision = plan.Revision, IdempotencyKey = Guid.NewGuid().ToString("N")
			});
			result.Loan.SchoolIssueDate.ShouldBe(input.IssueDate);
			return;
		}
		(await Should.ThrowAsync<BusinessException>(() => plans.PreviewAsync(input))).Code.ShouldBe(LibraryErrorCodes.InvalidIssueDateRange);
		(await Should.ThrowAsync<BusinessException>(() => loans.CheckoutAsync(new()
		{
			CopyId = input.CopyId, MemberId = input.MemberId, IssueDate = input.IssueDate,
			Revision = "Untrusted future date", IdempotencyKey = Guid.NewGuid().ToString("N")
		}))).Code.ShouldBe(LibraryErrorCodes.InvalidIssueDateRange);
		await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<BookIssue, Guid>>().AnyAsync(row => row.BookCopyId == input.CopyId)).ShouldBeFalse());
	}

	[Fact]
	public async Task Automated_Renewal_Should_Reject_A_Future_Business_Date_Without_Extending_The_Loan()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider));
		await GetRequiredService<ILibraryCalendarAppService>().UpdateSettingsAsync(new() { TimeZoneId = "Asia/Kolkata" });
		await PublishAsync(context.Catalog.YearId, 7, 1);
		var plan = await GetRequiredService<ILibraryIssuePlanAppService>().PreviewAsync(new()
		{
			CopyId = context.Catalog.CopyId, MemberId = context.StudentId, IssueDate = new(2026, 11, 17)
		});
		var loans = GetRequiredService<ILibraryLoanAppService>();
		var issued = await loans.CheckoutAsync(new()
		{
			CopyId = plan.CopyId, MemberId = plan.MemberId, IssueDate = plan.IssueDate,
			Revision = plan.Revision, IdempotencyKey = Guid.NewGuid().ToString("N")
		});
		var renewal = await loans.PreviewRenewalAsync(issued.Loan.Id, new() { SchoolRenewalDate = new(2027, 1, 1) });
		renewal.CanRenew.ShouldBeFalse();
		(await Should.ThrowAsync<BusinessException>(() => loans.RenewAsync(issued.Loan.Id, new()
		{
			SchoolRenewalDate = renewal.SchoolRenewalDate, Revision = renewal.Revision,
			IdempotencyKey = Guid.NewGuid().ToString("N")
		}))).Code.ShouldBe(LibraryCirculationErrorCodes.RenewalNotAllowed);
		var retained = await loans.GetAsync(issued.Loan.Id);
		retained.SchoolDueDate.ShouldBe(issued.Loan.SchoolDueDate);
		retained.RenewalCount.ShouldBe(0);
	}

	[Fact]
	public async Task Explicit_Manual_Historical_Checkout_And_Renewal_Should_Not_Infer_A_Timezone()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider));
		var policies = GetRequiredService<ILibraryBorrowingPolicyAppService>();
		var draft = await policies.CreateAsync(context.Catalog.YearId, new()
		{
			Name = "Explicit manual history", Role = 0, EffectiveFrom = new(2026, 11, 1),
			Terms = new() { DueDateMode = 0, MaxBooksAtTime = 2, ReservationLimit = 1, MaxRenewals = 1,
				LoanDays = 0, RenewalDays = 0, PickupWindowDays = 0 }
		});
		await policies.PublishAsync(draft.Id, new() { ConcurrencyStamp = draft.ConcurrencyStamp! });
		var plan = await GetRequiredService<ILibraryIssuePlanAppService>().PreviewAsync(new()
		{
			CopyId = context.Catalog.CopyId, MemberId = context.StudentId,
			IssueDate = new(2026, 11, 1), RequestedDueDate = new(2026, 11, 10)
		});
		var loans = GetRequiredService<ILibraryLoanAppService>();
		var issued = await loans.CheckoutAsync(new()
		{
			CopyId = plan.CopyId, MemberId = plan.MemberId, IssueDate = plan.IssueDate, RequestedDueDate = plan.DueDate,
			Revision = plan.Revision, IdempotencyKey = Guid.NewGuid().ToString("N")
		});
		var renewal = await loans.PreviewRenewalAsync(issued.Loan.Id, new()
		{
			SchoolRenewalDate = new(2026, 11, 9), RequestedDueDate = new(2026, 11, 20)
		});
		renewal.CanRenew.ShouldBeTrue();
		renewal.CurrentSchoolTimeZoneId.ShouldBeNull();
		var renewed = await loans.RenewAsync(issued.Loan.Id, new()
		{
			SchoolRenewalDate = renewal.SchoolRenewalDate, RequestedDueDate = renewal.NewSchoolDueDate,
			Revision = renewal.Revision, IdempotencyKey = Guid.NewGuid().ToString("N")
		});
		renewed.Loan.SchoolIssueDate.ShouldBe(new DateOnly(2026, 11, 1));
		renewed.Loan.OriginalSchoolDueDate.ShouldBe(new DateOnly(2026, 11, 10));
		renewed.Loan.SchoolDueDate.ShouldBe(new DateOnly(2026, 11, 20));
		renewed.Loan.RawIssueDate.ShouldBeNull();
	}
}

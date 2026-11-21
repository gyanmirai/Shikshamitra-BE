using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Policies;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Modularity;
using Volo.Abp.Timing;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryCanonicalRenewal_Tests : ShikshaMitraApplicationTestBase<LibraryCanonicalRenewalTestModule>
{
	[Fact]
	public async Task Renewals_Should_Use_Saved_Terms_And_Preserve_Original_Due_When_Todays_Policy_Changes()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var context = await WithUnitOfWorkAsync(() => LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider));
		await GetRequiredService<ILibraryCalendarAppService>().UpdateSettingsAsync(new() { TimeZoneId = "Asia/Kolkata" });
		var initial = await PublishAsync(context.Catalog.YearId, 7, 1);
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
		await GetRequiredService<ILibraryBorrowingPolicyAppService>().ArchiveAsync(initial.Id, new()
		{
			ConcurrencyStamp = initial.ConcurrencyStamp!, EffectiveOn = new(2026, 11, 17), Reason = "New terms apply to new loans."
		});
		await PublishAsync(context.Catalog.YearId, 30, 3, new(2026, 11, 17));
		var renewalPlan = await loans.PreviewRenewalAsync(issued.Loan.Id, new() { SchoolRenewalDate = new(2026, 11, 17) });

		var renewed = await loans.RenewAsync(issued.Loan.Id, new()
		{
			Revision = renewalPlan.Revision, IdempotencyKey = Guid.NewGuid().ToString("N"), SchoolRenewalDate = new(2026, 11, 17)
		});

		renewed.Loan.OriginalSchoolDueDate.ShouldBe(new DateOnly(2026, 11, 24));
		renewed.Loan.SchoolDueDate.ShouldBe(new DateOnly(2026, 12, 1));
		renewed.Loan.Policy!.PolicyVersionId.ShouldBe(initial.Id);
		renewed.Loan.RenewalCount.ShouldBe(1);
		var exhaustedPlan = await loans.PreviewRenewalAsync(renewed.Loan.Id, new() { SchoolRenewalDate = new(2026, 11, 17) });
		exhaustedPlan.CanRenew.ShouldBeFalse();
		(await Should.ThrowAsync<BusinessException>(() => loans.RenewAsync(renewed.Loan.Id, new()
		{
			Revision = exhaustedPlan.Revision, IdempotencyKey = Guid.NewGuid().ToString("N"), SchoolRenewalDate = new(2026, 11, 17)
		}))).Code.ShouldBe(LibraryCirculationErrorCodes.RenewalNotAllowed);
	}

	private async Task<LibraryBorrowingPolicyVersionDto> PublishAsync(Guid yearId, int days, int renewals, DateOnly? effectiveFrom = null)
	{
		var policies = GetRequiredService<ILibraryBorrowingPolicyAppService>();
		var draft = await policies.CreateAsync(yearId, new()
		{
			Name = "Saved loan terms", Role = 0, EffectiveFrom = effectiveFrom ?? new(2026, 11, 1),
			Terms = new() { MaxBooksAtTime = 4, ReservationLimit = 2, LoanDays = days, MaxRenewals = renewals, RenewalDays = days, PickupWindowDays = 2 }
		});
		return await policies.PublishAsync(draft.Id, new() { ConcurrencyStamp = draft.ConcurrencyStamp! });
	}
}

[DependsOn(typeof(ShikshaMitraApplicationTestModule))]
public class LibraryCanonicalRenewalTestModule : AbpModule
{
	public override void ConfigureServices(ServiceConfigurationContext context)
	{
		var clock = Substitute.For<IClock>();
		clock.Now.Returns(new DateTime(2026, 11, 17, 6, 0, 0, DateTimeKind.Utc));
		clock.Kind.Returns(DateTimeKind.Utc);
		clock.SupportsMultipleTimezone.Returns(true);
		clock.Normalize(Arg.Any<DateTime>()).Returns(call => call.Arg<DateTime>());
		context.Services.Replace(ServiceDescriptor.Singleton(clock));
	}
}

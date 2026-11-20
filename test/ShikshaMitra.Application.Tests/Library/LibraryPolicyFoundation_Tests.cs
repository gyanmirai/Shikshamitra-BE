using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Library.Policies;
using Shouldly;
using Volo.Abp;
using Volo.Abp.SettingManagement;
using Volo.Abp.Settings;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryPolicyFoundation_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private ILibraryBorrowingPolicyAppService Policies()
	{
		var service = ServiceProvider.GetService<ILibraryBorrowingPolicyAppService>();
		service.ShouldNotBeNull("Library policy resolution must be available to circulation.");
		return service;
	}

	private ILibraryCalendarAppService Calendar()
	{
		var service = ServiceProvider.GetService<ILibraryCalendarAppService>();
		service.ShouldNotBeNull("The Library calendar must expose configured school dates.");
		return service;
	}

	[Fact]
	public async Task No_Published_Policy_Should_Preserve_Legacy_Quotas_And_Manual_Dates()
	{
		var context = await ContextAsync();
		var snapshot = await Policies().ResolveAsync(Resolve(context));

		snapshot.IsLegacyManualDate.ShouldBeTrue();
		snapshot.PolicyVersionId.ShouldBeNull();
		snapshot.Terms.MaxBooksAtTime.ShouldBe(4);
		snapshot.Terms.ReservationLimit.ShouldBe(1);
		snapshot.Terms.LoanDays.ShouldBeNull();
		snapshot.Terms.RenewalDays.ShouldBeNull();
	}

	[Fact]
	public async Task Draft_Policy_Should_Not_Change_Existing_Borrowing_Behavior()
	{
		var context = await ContextAsync();
		await Policies().CreateAsync(context.YearId, Draft());

		(await Policies().ResolveAsync(Resolve(context))).IsLegacyManualDate.ShouldBeTrue();
	}

	[Fact]
	public async Task Publishing_Should_Require_Explicit_Library_Timezone_Confirmation()
	{
		var context = await ContextAsync();
		var policy = await Policies().CreateAsync(context.YearId, Draft());

		await Should.ThrowAsync<BusinessException>(() => Policies().PublishAsync(policy.Id,
			new() { ConcurrencyStamp = policy.ConcurrencyStamp! }));
	}

	[Fact]
	public async Task Published_Terms_Should_Remain_Immutable()
	{
		var context = await ContextAsync();
		await Calendar().UpdateSettingsAsync(new() { TimeZoneId = "America/New_York" });
		var policy = await PublishAsync(context.YearId, Draft());
		var change = Draft();
		change.Name = "Changed published terms";
		change.ConcurrencyStamp = policy.ConcurrencyStamp;

		await Should.ThrowAsync<BusinessException>(() => Policies().UpdateAsync(policy.Id, change));
		(await Policies().GetAsync(policy.Id)).Terms.LoanDays.ShouldBe(7);
	}

	[Fact]
	public async Task Publishing_Should_Reject_Overlapping_Equal_Precedence_Rules()
	{
		var context = await ContextAsync();
		await Calendar().UpdateSettingsAsync(new() { TimeZoneId = "America/New_York" });
		await PublishAsync(context.YearId, Draft());
		var second = await Policies().CreateAsync(context.YearId, Draft());

		await Should.ThrowAsync<BusinessException>(() => Policies().PublishAsync(second.Id,
			new() { ConcurrencyStamp = second.ConcurrencyStamp! }));
	}

	[Fact]
	public async Task More_Specific_Rule_Should_Win_Deterministically()
	{
		var context = await ContextAsync();
		await Calendar().UpdateSettingsAsync(new() { TimeZoneId = "America/New_York" });
		await PublishAsync(context.YearId, Draft());
		var specific = Draft();
		specific.BookCategoryId = context.CategoryId;
		specific.Terms.LoanDays = 3;
		var published = await PublishAsync(context.YearId, specific);

		var snapshot = await Policies().ResolveAsync(Resolve(context));

		snapshot.PolicyVersionId.ShouldBe(published.Id);
		snapshot.Terms.LoanDays.ShouldBe(3);
		snapshot.IsLegacyManualDate.ShouldBeFalse();
	}

	[Fact]
	public async Task Scheduled_Successor_Should_Preserve_Previous_Date_Resolution()
	{
		var context = await ContextAsync();
		await Calendar().UpdateSettingsAsync(new() { TimeZoneId = "America/New_York" });
		var first = await PublishAsync(context.YearId, Draft());
		await Policies().ArchiveAsync(first.Id, new()
		{
			ConcurrencyStamp = first.ConcurrencyStamp!, EffectiveOn = new(2026, 11, 20), Reason = "Scheduled new term"
		});
		var replacement = Draft();
		replacement.PolicyId = first.PolicyId;
		replacement.EffectiveFrom = new(2026, 11, 20);
		var second = await PublishAsync(context.YearId, replacement);
		var before = Resolve(context);
		before.AsOfDate = new(2026, 11, 19);
		var after = Resolve(context);
		after.AsOfDate = new(2026, 11, 20);

		(await Policies().ResolveAsync(before)).PolicyVersionId.ShouldBe(first.Id);
		(await Policies().ResolveAsync(after)).PolicyVersionId.ShouldBe(second.Id);
	}

	[Fact]
	public async Task Open_Day_Calculation_Should_Skip_Weekend_And_Dated_Closure()
	{
		var context = await ContextAsync();
		await Calendar().UpdateSettingsAsync(new() { TimeZoneId = "America/New_York" });
		await Calendar().CreateClosureAsync(context.YearId, new()
		{
			FromDate = new(2026, 11, 16), ToDate = new(2026, 11, 16), Reason = "School closure"
		});

		var result = await Calendar().CalculateAsync(new()
		{
			AcademicYearId = context.YearId, StartDate = new(2026, 11, 13), Days = 2, DayCountingMode = 1
		});

		result.DueDate.ShouldBe(new DateOnly(2026, 11, 18));
		result.TimeZoneId.ShouldBe("America/New_York");
		result.NeedsConfiguration.ShouldBeFalse();
	}

	[Fact]
	public async Task Library_Timezone_Setup_Should_Not_Change_Hr_Timezone()
	{
		await ContextAsync();
		await WithUnitOfWorkAsync(() => GetRequiredService<ISettingManager>()
			.SetGlobalAsync("ShikshaMitra.HR.Reporting.TimeZone", "Pacific/Auckland"));

		var result = await Calendar().UpdateSettingsAsync(new() { TimeZoneId = "America/New_York" });

		result.Source.ShouldBe("Library");
		result.TimeZoneId.ShouldBe("America/New_York");
		(await GetRequiredService<ISettingProvider>().GetOrNullAsync("ShikshaMitra.HR.Reporting.TimeZone")).ShouldBe("Pacific/Auckland");
	}

	private Task<LibraryPolicyTestContext> ContextAsync() =>
		WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));

	private async Task<LibraryBorrowingPolicyVersionDto> PublishAsync(Guid yearId, LibraryBorrowingPolicyDraftRequest input)
	{
		var draft = await Policies().CreateAsync(yearId, input);
		return await Policies().PublishAsync(draft.Id, new() { ConcurrencyStamp = draft.ConcurrencyStamp! });
	}

	private static LibraryBorrowingPolicyDraftRequest Draft() => new()
	{
		Name = "Student borrowing", Role = 0, EffectiveFrom = new(2026, 11, 1),
		Terms = new()
		{
			MaxBooksAtTime = 2, ReservationLimit = 2, LoanDays = 7, DayCountingMode = 0,
			MaxRenewals = 1, RenewalDays = 7, PickupWindowDays = 2
		}
	};

	private static LibraryPolicyResolveRequest Resolve(LibraryPolicyTestContext context) => new()
	{
		AcademicYearId = context.YearId, Role = 0, BookId = context.BookId,
		CopyId = context.CopyId, AsOfDate = new(2026, 11, 16)
	};
}

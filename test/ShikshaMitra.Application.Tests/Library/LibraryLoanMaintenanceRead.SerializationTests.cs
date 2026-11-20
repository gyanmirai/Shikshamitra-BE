using System;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Policies;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryLoanMaintenanceRead_Tests
{
	[Fact]
	public async Task Loan_Maintenance_Read_Should_Not_Change_Command_Results_Or_Stored_Replay_Json()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await WithUnitOfWorkAsync<LibraryCopyLifecycleTestContext>(() =>
			LibraryCopyLifecycleTestFixture.SeedAsync(ServiceProvider));
		var input = new LibraryCheckoutRequest
		{
			CopyId = fixture.Catalog.CopyId, MemberId = fixture.StudentId, IssueDate = new(2026, 11, 17),
			RequestedDueDate = new(2026, 11, 24), IdempotencyKey = Guid.NewGuid().ToString("N")
		};
		input.Revision = (await GetRequiredService<ILibraryIssuePlanAppService>().PreviewAsync(new()
		{
			CopyId = input.CopyId, MemberId = input.MemberId, IssueDate = input.IssueDate, RequestedDueDate = input.RequestedDueDate
		})).Revision;
		var accepted = await Loans.CheckoutAsync(input);
		accepted.WasReplay.ShouldBeFalse();
		WithoutGetFields(JsonSerializer.SerializeToElement(accepted.Loan, WireJson));
		var stored = await StoredCommandResultAsync(input.IdempotencyKey);
		using (var saved = JsonDocument.Parse(stored)) WithoutGetFields(saved.RootElement.GetProperty("Loan"));
		var preview = await Loans.PreviewRenewalAsync(accepted.Loan.Id, new() { SchoolRenewalDate = new(2026, 11, 17) });
		WithoutGetFields(JsonSerializer.SerializeToElement(preview.Loan, WireJson));

		var initial = await Loans.GetAsync(accepted.Loan.Id);
		initial.Revision.ShouldBe(accepted.Loan.Revision);
		ContextOf(initial).IsConfirmed.ShouldBeFalse();
		await SetZoneAsync(LibraryPolicySettingNames.CalendarTimeZone, "Asia/Kolkata");
		var refreshed = await Loans.GetAsync(accepted.Loan.Id);
		refreshed.Revision.ShouldBe(initial.Revision);
		ContextOf(refreshed).SchoolToday.ShouldBe(new DateOnly(2026, 11, 18));
		CanReviewDatesOf(refreshed).ShouldBeFalse();
		(await StoredCommandResultAsync(input.IdempotencyKey)).ShouldBe(stored);

		var replay = await Loans.CheckoutAsync(input);

		replay.WasReplay.ShouldBeTrue(); replay.Loan.Id.ShouldBe(accepted.Loan.Id);
		replay.Loan.Revision.ShouldBe(accepted.Loan.Revision);
		WithoutGetFields(JsonSerializer.SerializeToElement(replay.Loan, WireJson));
		(await StoredCommandResultAsync(input.IdempotencyKey)).ShouldBe(stored);
	}

	[Fact]
	public async Task Loan_Maintenance_Read_Should_Keep_The_Existing_Return_Lookup_Nested_Loan_Shape()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedLegacyLoanAsync();
		var accession = await WithUnitOfWorkAsync<string>(async () =>
			(await GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(fixture.Catalog.CopyId)).AccessionNumber);

		var lookup = await GetRequiredService<ILibraryCirculationLookupAppService>().GetReturnAsync(accession);

		lookup.Loan.Id.ShouldBe(fixture.LoanId!.Value);
		WithoutGetFields(JsonSerializer.SerializeToElement(lookup.Loan, WireJson));
		lookup.SchoolContext.AcademicYears.ShouldNotBeEmpty();
		lookup.Copy.SchoolContext.AcademicYears.ShouldNotBeEmpty();
		var direct = await Loans.GetAsync(lookup.Loan.Id);
		ContextOf(direct).AcademicYears.ShouldNotBeEmpty();
		CanReviewDatesOf(direct).ShouldBeTrue();
	}

	private Task<string> StoredCommandResultAsync(string key) => WithUnitOfWorkAsync<string>(async () =>
		(await GetRequiredService<IRepository<LibraryLoanCommand, Guid>>().GetAsync(row => row.IdempotencyKey == key)).ResultJson);

	private static void WithoutGetFields(JsonElement loan)
	{
		foreach (var name in new[] { "schoolContext", "SchoolContext", "canReviewDates", "CanReviewDates" })
			loan.TryGetProperty(name, out _).ShouldBeFalse("Fresh GET context must not enter a command, replay or existing nested lookup payload.");
	}
}

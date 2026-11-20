using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Library.Reports;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Shared;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryStaffRecordedScope_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task H_ReadEvidence_Report_Should_Keep_Confirmed_Civil_Dates_Distinct_From_Unconfirmed_Raw_Values(bool confirmed)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			var copy = await GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(fixture.Context.Catalog.CopyId);
			var loan = confirmed ? OperationalCivilLoan(fixture, copy, new(2026, 11, 17), new(2026, 11, 24)) :
				new BookIssue(Guid.NewGuid(), copy.Id, copy.AccessionNumber, fixture.Context.StudentId, 0,
					new(2001, 1, 1), new(2001, 1, 2), copy.TenantId);
			if (confirmed) loan.ReceivePhysicalCopy(new(2026, 11, 25), reviewedUnknownDates: false);
			await GetRequiredService<IRepository<BookIssue, Guid>>().InsertAsync(loan, true);
		});

		var report = await ReadOperationalReportAsync(LibraryReportTypes.IssueReturn);
		var row = report.Rows.Single();

		row.TryGetValue("schoolIssueDate", out var schoolIssue).ShouldBeTrue();
		schoolIssue.ShouldBe(confirmed ? "2026-11-17" : string.Empty);
		row["schoolDueDate"].ShouldBe(confirmed ? "2026-11-24" : string.Empty);
		row["schoolReturnDate"].ShouldBe(confirmed ? "2026-11-25" : string.Empty);
		row["dueDateProvenance"].ShouldBe(confirmed ? "1" : "0");
		row["issueDate"].ShouldBe(confirmed ? string.Empty : "2001-01-01");
		row["rawIssueDate"].ShouldBe(confirmed ? string.Empty : new DateTime(2001, 1, 1).ToString("O", CultureInfo.InvariantCulture));
	}

	[Fact]
	public async Task H_ReadEvidence_Report_Should_Show_The_Actual_Cancelled_Request_And_Civil_Dates_Without_Reinterpreting_Raw_Expiry()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			var context = fixture.Context;
			var request = BookReservation.Create(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id, context.Catalog.BookId,
				context.StudentId, context.StudentId, 0, context.Catalog.YearId, 1, new(2026, 11, 10), new(2026, 11, 18), "{}",
				new DateTime(2001, 1, 1), new DateTime(2100, 1, 1), new(context.BoardId, context.MediumId, context.StageId, context.BorrowerClassId, null, "{}"));
			request.Finish(LibraryReservationState.Cancelled, "Borrower cancelled the request", new(2026, 11, 17, 9, 0, 0, TimeSpan.Zero));
			await GetRequiredService<IRepository<BookReservation, Guid>>().InsertAsync(request, true);
		});

		var report = await ReadOperationalReportAsync(LibraryReportTypes.ReservationList);
		var row = report.Rows.Single();

		row["status"].ShouldBe("Cancelled");
		row["schoolRequestedDate"].ShouldBe("2026-11-10");
		row["schoolRequestExpiryDate"].ShouldBe("2026-11-18");
		row["requestedDate"].ShouldBe("2001-01-01");
		row["reserveDueDate"].ShouldBe("2100-01-01");
	}

	[Theory]
	[InlineData("UTC", 1, "2026-11-18")]
	[InlineData("Pacific/Honolulu", 0, "2026-11-17")]
	public async Task H_ReadEvidence_Overdue_Report_Should_Use_Confirmed_Due_Dates_And_Expose_The_Library_School_Day(string zone, int count, string schoolDay)
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		await SeedStatisticsLoanAsync(fixture, new(2026, 11, 17));

		var report = await ReadEvidenceReportAsync(zone);

		report.TotalCount.ShouldBe(count);
		report.OverdueClassificationAvailable.ShouldBeTrue();
		report.UnknownDueDateCount.ShouldBe(0);
		report.OverdueAsOfDate.ShouldBe(DateOnly.ParseExact(schoolDay, "yyyy-MM-dd", CultureInfo.InvariantCulture));
		report.OverdueDateSource.ShouldBe("ConfirmedLibraryClock");
	}

	[Fact]
	public async Task H_ReadEvidence_Overdue_Report_Should_Allow_An_Explicit_School_AsOf_Date_Without_A_Configured_Clock()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		await SeedStatisticsLoanAsync(fixture, new(2026, 11, 17));

		var report = await ReadEvidenceReportAsync(null, new(2026, 11, 18));

		report.TotalCount.ShouldBe(1);
		report.OverdueClassificationAvailable.ShouldBeTrue();
		report.OverdueDateSource.ShouldBe("ExplicitSchoolDate");
		report.OverdueAsOfDate.ShouldBe(new DateOnly(2026, 11, 18));
	}

	[Fact]
	public async Task H_ReadEvidence_Overdue_Report_Should_Not_Classify_An_Unconfirmed_Raw_Due_Timestamp()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			var copy = await GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(fixture.Context.Catalog.CopyId);
			var loan = new BookIssue(Guid.NewGuid(), copy.Id, copy.AccessionNumber, fixture.Context.StudentId, 0,
				new(2001, 1, 1), new(2001, 1, 2), copy.TenantId);
			loan.SchoolDueDate.ShouldBeNull();
			await GetRequiredService<IRepository<BookIssue, Guid>>().InsertAsync(loan, true);
		});

		var report = await ReadEvidenceReportAsync("UTC");

		report.TotalCount.ShouldBe(0);
		report.UnknownDueDateCount.ShouldBe(1);
		report.OverdueClassificationAvailable.ShouldBeFalse();
		report.OverdueUnavailableReason.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task H_ReadEvidence_Overdue_Report_Without_A_Confirmed_Clock_Should_Expose_Unavailable_Classification()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var fixture = await SeedStaffAsync();
		await SeedStatisticsLoanAsync(fixture, new(2026, 11, 17));

		var report = await ReadEvidenceReportAsync(null);

		report.TotalCount.ShouldBe(0);
		report.OverdueClassificationAvailable.ShouldBeFalse();
		report.OverdueUnavailableReason.ShouldNotBeNullOrWhiteSpace();
		report.SchoolAsOfDate.ShouldBeNull();
		report.SchoolDateSource.ShouldBe("Unavailable");
	}

	private async Task<LibraryReportPreviewDto> ReadEvidenceReportAsync(string? zone, DateOnly? asOfDate = null)
	{
		if (zone != null) await WithUnitOfWorkAsync(() => GetRequiredService<ISettingManager>().SetForTenantAsync(
			GetRequiredService<ICurrentTenant>().Id!.Value, LibraryPolicySettingNames.CalendarTimeZone, zone, forceToSet: true));
		return await ReadOperationalReportAsync(LibraryReportTypes.OverdueBooks,
			services: new OperationalClockServices(ServiceProvider, new(2026, 11, 18, 0, 30, 0, DateTimeKind.Utc)), asOfDate: asOfDate);
	}
}

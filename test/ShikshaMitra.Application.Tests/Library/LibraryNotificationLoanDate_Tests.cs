using System;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Notifications;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Volo.Abp.Timing;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryNotificationRecipientAccess_Tests
{
	[Fact]
	public async Task Notification_Loan_Reminders_Should_Not_Treat_Unconfirmed_Raw_Timestamps_As_School_Dates()
	{
		var context = await SeedAsync(); using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		var loanId = await SeedUnconfirmedNotificationLoanAsync(context);
		var rows = await WithUnitOfWorkAsync(() => Services().Get<LibraryNotificationReminderService>().ProcessDueRemindersAsync(new DateTime(2026, 11, 16)));
		rows.ShouldNotContain(row => row.RelatedEntityId == loanId &&
			(row.EventType == LibraryNotificationEventTypes.DueDateReminder || row.EventType == LibraryNotificationEventTypes.OverdueReminder) &&
			(row.Status == LibraryNotificationDeliveryStatuses.Queued || row.Status == LibraryNotificationDeliveryStatuses.Sent));
	}

	[Fact]
	public async Task Notification_Loan_Reminders_Should_Use_Confirmed_Civil_Dates_When_Raw_Dates_Are_Null()
	{
		var context = await SeedAsync(); using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		var loanId = await SeedCanonicalNotificationLoanAsync(context);
		var rows = await WithUnitOfWorkAsync(() => Services().Get<LibraryNotificationReminderService>().ProcessDueRemindersAsync(new DateTime(2026, 11, 16)));
		rows.ShouldContain(row => row.RelatedEntityId == loanId && row.EventType == LibraryNotificationEventTypes.DueDateReminder &&
			row.Message.Contains("2026-11-18") && (row.Status == LibraryNotificationDeliveryStatuses.Queued || row.Status == LibraryNotificationDeliveryStatuses.Sent));
	}

	[Fact]
	public async Task Notification_Loan_Deadline_Recheck_Should_Use_Confirmed_Date_Despite_A_Later_Raw_Timestamp()
	{
		var context = await SeedAsync(); using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		await WithUnitOfWorkAsync(() => GetRequiredService<ISettingManager>().SetForTenantAsync(context.TenantId,
			LibraryPolicySettingNames.CalendarTimeZone, "UTC", forceToSet: true));
		var loanId = await SeedCanonicalNotificationLoanAsync(context, new DateTime(2026, 11, 30));
		var services = Services(new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc));
		var eligibility = services.Get<LibraryNotificationReminderEligibility>();
		var intent = new LibraryNotificationHistory(Guid.NewGuid(), context.TenantId, LibraryNotificationEventTypes.DueDateReminder,
			LibraryNotificationEventGroups.Transactions, "Due date reminder", LibraryNotificationChannels.Email,
			LibraryNotificationRecipientRoles.Member, context.StudentId, "Library reader", "guardian@example.test",
			"Return reminder", "Due on the confirmed school date.", LibraryNotificationDeliveryStatuses.Queued,
			null, nameof(BookIssue), loanId, null, new DateTime(2026, 11, 16), null);
		(await WithUnitOfWorkAsync(() => eligibility.CaptureAsync(intent))).ShouldBeTrue();
		services.Get<IClock>().Now.Returns(new DateTime(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc));
		(await WithUnitOfWorkAsync(() => eligibility.IsCurrentAsync(intent))).ShouldBeFalse();
	}

	private Task<Guid> SeedUnconfirmedNotificationLoanAsync(Context context) => WithUnitOfWorkAsync(async () => {
		var loan = new BookIssue(Guid.NewGuid(), context.CopyId, "NOTIFY-COPY", context.StudentId, 0,
			new DateTime(2026, 11, 10), new DateTime(2026, 11, 18), context.TenantId);
		await GetRequiredService<IRepository<BookIssue, Guid>>().InsertAsync(loan, true);
		var copies = GetRequiredService<IRepository<BookCopy, Guid>>(); var copy = await copies.GetAsync(context.CopyId);
		copy.Status = BookCopyStatus.Issued; await copies.UpdateAsync(copy, true);
		return loan.Id;
	});

	private Task<Guid> SeedCanonicalNotificationLoanAsync(Context context, DateTime? conflictingRawDue = null) => WithUnitOfWorkAsync(async () => {
		var student = await GetRequiredService<IRepository<Student, Guid>>().GetAsync(context.StudentId);
		var copies = GetRequiredService<IRepository<BookCopy, Guid>>(); var copy = await copies.GetAsync(context.CopyId);
		var loan = BookIssue.CreateCivil(Guid.NewGuid(), context.TenantId, copy.Id, copy.AccessionNumber, context.StudentId, 0,
			new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 18), new LibraryLoanIssueContext(student.AcademicYearId!.Value,
				student.AcademicBoardId, student.AcademicMediumId, student.AcademicMediumStageId, student.StandardId,
				student.DivisionId, "{}", "{}", "{}", context.ActorId, new DateTimeOffset(2026, 11, 10, 3, 0, 0, TimeSpan.Zero)));
		loan.DueDate = conflictingRawDue;
		await GetRequiredService<IRepository<BookIssue, Guid>>().InsertAsync(loan, true);
		copy.Status = BookCopyStatus.Issued; await copies.UpdateAsync(copy, true);
		return loan.Id;
	});
}

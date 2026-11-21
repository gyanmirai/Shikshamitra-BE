using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Library.Notifications;

public partial class LibraryNotificationReminderService(
	IRepository<Book, Guid> bookRepository,
	IRepository<BookCopy, Guid> copyRepository,
	IRepository<BookIssue, Guid> issueRepository,
	LibraryFineReminderEvidenceReader fineEvidence,
	LibraryNotificationRecipientResolver recipientResolver,
	LibraryNotificationDeliveryService deliveryService,
	LibraryNotificationPreferences preferences,
	LibraryReservationNotificationEvidenceReader reservationEvidence,
	LibraryNotificationCandidateScope candidates,
	Volo.Abp.MultiTenancy.ICurrentTenant currentTenant,
	ILibraryNotificationAuthorityService authority,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	private const int LowStockThreshold = 2;

	public async Task<List<LibraryNotificationHistory>> ProcessDueRemindersAsync(DateTime? runDate = null, IReadOnlySet<Guid>? allowedMemberIds = null,
		LibraryNotificationOrigin? origin = null)
	{
		origin ??= LibraryNotificationOrigin.Scheduled;
		if (!runDate.HasValue)
		{
			var schedule = await preferences.GetAutomaticDateAsync();
			if (schedule.BlockedReason != null) return await deliveryService.RecordSetupSkippedAsync(schedule.BlockedReason, schedule.ConfigurationFingerprint);
			if (!schedule.Date.HasValue) return [];
			runDate = schedule.Date;
		}
		var today = runDate.Value.Date;
		var settings = await deliveryService.EnsureSettingsAsync();
		var histories = new List<LibraryNotificationHistory>();

		histories.AddRange(await ProcessDueIssueRemindersAsync(today, settings, allowedMemberIds, origin));
		histories.AddRange(await ProcessOverdueIssueRemindersAsync(today, allowedMemberIds, origin));
		histories.AddRange(await ProcessFineDueRemindersAsync(today, origin));
		histories.AddRange(await ProcessReservationAvailabilityRemindersAsync(today, allowedMemberIds, origin));
		histories.AddRange(await ProcessReservationExpiringRemindersAsync(today, settings, allowedMemberIds, origin));
		if (allowedMemberIds == null) histories.AddRange(await ProcessLowStockAlertsAsync(today, origin));

		return histories;
	}

	public async Task<List<LibraryNotificationHistory>> SendManualAsync(SendLibraryNotificationRequest input)
	{
		if (input.Recipients.Count == 0)
		{
			throw new Volo.Abp.BusinessException(code: LibraryErrorCodes.NotificationRecipientRequired, message: "At least one notification recipient is required.");
		}

		var eventType = input.EventType;
		var recipients = new List<LibraryNotificationRecipient>();
		foreach (var recipient in input.Recipients)
		{
			if (recipient.MemberId.HasValue)
			{
				recipients.Add(await recipientResolver.ResolveMemberAsync(recipient.MemberId.Value, fallbackName: recipient.RecipientName, fallbackEmail: recipient.RecipientEmail));
				continue;
			}

			recipients.Add(new LibraryNotificationRecipient(
				MemberId: null,
				RecipientRole: recipient.RecipientRole ?? LibraryNotificationRecipientRoles.Member,
				RecipientName: recipient.RecipientName,
				Email: recipient.RecipientEmail,
				Phone: null,
				IsActiveLibraryMember: true));
		}

		return await deliveryService.DeliverAsync(new LibraryNotificationDeliveryRequest(
			eventType,
			new[] { input.Channel },
			recipients,
			new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase),
			input.Subject,
			string.IsNullOrWhiteSpace(input.Message) ? null : input.Message,
			input.RelatedEntityType,
			input.RelatedEntityId,
			ReminderDate: null,
			DeduplicationKey: null,
			Origin: authority.ForCurrentUser(ShikshaMitraSubmodulePermissions.Library.Notification.Add)));
	}

	public async Task<List<LibraryNotificationHistory>> SendIssueConfirmationAsync(BookIssue issue, Book book, BookCopy copy)
	{
		var recipient = await recipientResolver.ResolveMemberAsync(issue.MemberId, issue.MemberRole);
		return await deliveryService.DeliverAsync(new LibraryNotificationDeliveryRequest(
			LibraryNotificationEventTypes.BookIssueConfirmation,
			RealTimeChannels(),
			new[] { recipient },
			BuildIssuePlaceholders(issue, book, copy),
			SubjectOverride: null,
			MessageOverride: null,
			RelatedEntityType: nameof(BookIssue),
			RelatedEntityId: issue.Id,
			ReminderDate: LibraryLoanDates.ConfirmedIssueDate(issue)?.ToDateTime(TimeOnly.MinValue),
			DeduplicationKey: null,
			Origin: authority.ForCurrentUser(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Issue)));
	}

	public async Task<List<LibraryNotificationHistory>> SendReturnNotificationsAsync(BookIssue issue, Book? book, BookCopy? copy)
	{
		var recipient = await recipientResolver.ResolveMemberAsync(issue.MemberId, issue.MemberRole);
		var placeholders = BuildIssuePlaceholders(issue, book, copy);
		placeholders["returnDate"] = FormatSchoolDate(LibraryLoanDates.ConfirmedReturnDate(issue));
		placeholders["amount"] = FormatAmount(issue.Amount);
		placeholders["fineDueDate"] = FormatDate(issue.FineDueDate);

		var histories = await deliveryService.DeliverAsync(new LibraryNotificationDeliveryRequest(
			LibraryNotificationEventTypes.ReturnConfirmation,
			RealTimeChannels(),
			new[] { recipient },
			placeholders,
			SubjectOverride: null,
			MessageOverride: null,
			RelatedEntityType: nameof(BookIssue),
			RelatedEntityId: issue.Id,
			ReminderDate: LibraryLoanDates.ConfirmedReturnDate(issue)?.ToDateTime(TimeOnly.MinValue),
			DeduplicationKey: null,
			Origin: authority.ForCurrentUser(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Return)));

		if (issue.Amount is > 0)
		{
			histories.AddRange(await deliveryService.DeliverAsync(new LibraryNotificationDeliveryRequest(
				LibraryNotificationEventTypes.ReturnChargeReminder,
				RealTimeChannels(),
				new[] { recipient },
				placeholders,
				SubjectOverride: null,
				MessageOverride: null,
				RelatedEntityType: nameof(BookIssue),
				RelatedEntityId: issue.Id,
				ReminderDate: issue.FineDueDate?.Date ?? LibraryLoanDates.ConfirmedReturnDate(issue)?.ToDateTime(TimeOnly.MinValue),
				DeduplicationKey: null,
				Origin: authority.ForCurrentUser(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Return))));
		}

		return histories;
	}

	private async Task<List<LibraryNotificationHistory>> ProcessDueIssueRemindersAsync(DateTime today, List<LibraryNotificationSetting> settings,
		IReadOnlySet<Guid>? allowedMemberIds, LibraryNotificationOrigin origin)
	{
		var histories = new List<LibraryNotificationHistory>();
		foreach (var group in OffsetGroups(settings, LibraryNotificationEventTypes.DueDateReminder, 2))
		{
			var targetDueDate = DateOnly.FromDateTime(today).AddDays(group.Key);
			var issues = await GetActiveIssuesAsync(LibraryLoanDates.DueOn(targetDueDate), allowedMemberIds, origin);
			histories.AddRange(await SendIssueReminderBatchAsync(issues, LibraryNotificationEventTypes.DueDateReminder, today, origin, group.Select(setting => setting.Channel).ToArray()));
		}
		return histories;
	}

	private async Task<List<LibraryNotificationHistory>> ProcessOverdueIssueRemindersAsync(DateTime today, IReadOnlySet<Guid>? allowedMemberIds, LibraryNotificationOrigin origin)
	{
		var issues = await GetActiveIssuesAsync(LibraryLoanDates.DueBefore(DateOnly.FromDateTime(today)), allowedMemberIds, origin);
		return await SendIssueReminderBatchAsync(issues, LibraryNotificationEventTypes.OverdueReminder, today, origin);
	}

	private async Task<List<LibraryNotificationHistory>> ProcessLowStockAlertsAsync(DateTime today, LibraryNotificationOrigin origin)
	{
		var books = await asyncExecuter.ToListAsync(await bookRepository.GetQueryableAsync());
		var copies = await asyncExecuter.ToListAsync(await copyRepository.GetQueryableAsync());
		var histories = new List<LibraryNotificationHistory>();

		foreach (var book in books)
		{
			var bookCopies = copies.Where(x => x.BookId == book.Id).ToList();
			if (bookCopies.Count == 0)
			{
				continue;
			}

			var availableCopies = bookCopies.Count(x => x.Status == BookCopyStatus.Available);
			if (availableCopies > LowStockThreshold)
			{
				continue;
			}

			histories.AddRange(await deliveryService.DeliverAsync(new LibraryNotificationDeliveryRequest(
				LibraryNotificationEventTypes.LowStockAlert,
				RealTimeChannels(),
				new[] { LibraryNotificationRecipientResolver.BuildOperationalRecipient("Library administrators") },
				new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
				{
					["bookTitle"] = book.Title,
					["availableCopies"] = availableCopies.ToString(CultureInfo.InvariantCulture),
					["totalCopies"] = bookCopies.Count.ToString(CultureInfo.InvariantCulture)
				},
				SubjectOverride: null,
				MessageOverride: null,
				RelatedEntityType: nameof(Book),
				RelatedEntityId: book.Id,
				ReminderDate: today,
				DeduplicationKey: null, Origin: origin)));
		}

		return histories;
	}

	private async Task<List<LibraryNotificationHistory>> SendIssueReminderBatchAsync(
		IReadOnlyCollection<BookIssue> issues,
		string eventType,
		DateTime today, LibraryNotificationOrigin origin, IReadOnlyCollection<string>? channels = null)
	{
		var histories = new List<LibraryNotificationHistory>();
		foreach (var issue in issues)
		{
			var copy = await copyRepository.FindAsync(issue.BookCopyId);
			var book = copy == null ? null : await bookRepository.FindAsync(copy.BookId);
			var recipient = await recipientResolver.ResolveMemberAsync(issue.MemberId, issue.MemberRole);

			histories.AddRange(await deliveryService.DeliverAsync(new LibraryNotificationDeliveryRequest(
				eventType,
				channels ?? RealTimeChannels(),
				new[] { recipient },
				BuildIssuePlaceholders(issue, book, copy),
				SubjectOverride: null,
				MessageOverride: null,
				RelatedEntityType: nameof(BookIssue),
				RelatedEntityId: issue.Id,
				ReminderDate: today,
				DeduplicationKey: null, Origin: origin)));
		}

		return histories;
	}

	private async Task<List<BookIssue>> GetActiveIssuesAsync(Expression<Func<BookIssue, bool>> predicate, IReadOnlySet<Guid>? allowedMemberIds,
		LibraryNotificationOrigin origin)
	{
		var query = await candidates.GetLoansAsync(origin);
		if (origin.IsSystem && allowedMemberIds != null)
		{
			var memberIds = allowedMemberIds.ToArray();
			query = query.Where(x => Enumerable.Contains(memberIds, x.MemberId));
		}
		return await asyncExecuter.ToListAsync(query.Where(x => x.Status == BookIssueStatus.Issued).Where(predicate));
	}

	private async Task<Book?> ResolveBookForChargeAsync(LibraryCharge charge)
	{
		if (!charge.BookIssueId.HasValue)
		{
			return null;
		}

		var issue = await issueRepository.FindAsync(charge.BookIssueId.Value);
		if (issue == null)
		{
			return null;
		}

		var copy = await copyRepository.FindAsync(issue.BookCopyId);
		return copy == null ? null : await bookRepository.FindAsync(copy.BookId);
	}

	private static Dictionary<string, string?> BuildIssuePlaceholders(BookIssue issue, Book? book, BookCopy? copy) =>
		new(StringComparer.OrdinalIgnoreCase)
		{
			["bookTitle"] = book?.Title ?? "Library item",
			["accessionNumber"] = issue.AccessionNumber,
			["copyNumber"] = copy?.CopyNumber.ToString(CultureInfo.InvariantCulture),
			["issueDate"] = FormatSchoolDate(LibraryLoanDates.ConfirmedIssueDate(issue)),
			["dueDate"] = FormatSchoolDate(LibraryLoanDates.ConfirmedDueDate(issue))
		};

	private static IReadOnlyCollection<string> RealTimeChannels() =>
		new[] { LibraryNotificationChannels.InApp, LibraryNotificationChannels.Email };

	private static IEnumerable<IGrouping<int, LibraryNotificationSetting>> OffsetGroups(IEnumerable<LibraryNotificationSetting> settings, string eventType, int fallback) =>
		settings.Where(setting => setting.EventType == eventType && setting.IsActive && setting.IsChannelAvailable && !setting.IsComingSoon)
			.GroupBy(setting => setting.ReminderOffsetDays ?? fallback);

	private static string FormatDate(DateTime? date) =>
		date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;

	private static string FormatSchoolDate(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;

	private static string FormatAmount(decimal? amount) =>
		amount?.ToString("0.##", CultureInfo.InvariantCulture) ?? "0";
}

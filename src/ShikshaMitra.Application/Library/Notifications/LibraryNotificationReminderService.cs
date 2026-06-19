using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Library.Notifications;

public class LibraryNotificationReminderService(
	IRepository<Book, Guid> bookRepository,
	IRepository<BookCopy, Guid> copyRepository,
	IRepository<BookIssue, Guid> issueRepository,
	IRepository<BookReservation, Guid> reservationRepository,
	IRepository<LibraryCharge, Guid> chargeRepository,
	LibraryNotificationRecipientResolver recipientResolver,
	LibraryNotificationDeliveryService deliveryService,
	IAsyncQueryableExecuter asyncExecuter)
	: ITransientDependency
{
	private const int LowStockThreshold = 2;

	public async Task<List<LibraryNotificationHistory>> ProcessDueRemindersAsync(DateTime? runDate = null)
	{
		var today = (runDate ?? DateTime.UtcNow).Date;
		var settings = await deliveryService.EnsureSettingsAsync();
		var histories = new List<LibraryNotificationHistory>();

		histories.AddRange(await ProcessDueIssueRemindersAsync(today, GetReminderOffset(settings, LibraryNotificationEventTypes.DueDateReminder, 2)));
		histories.AddRange(await ProcessOverdueIssueRemindersAsync(today));
		histories.AddRange(await ProcessFineDueRemindersAsync(today));
		histories.AddRange(await ProcessReservationAvailabilityRemindersAsync(today));
		histories.AddRange(await ProcessReservationExpiringRemindersAsync(today, GetReminderOffset(settings, LibraryNotificationEventTypes.ReservationExpiring, 1)));
		histories.AddRange(await ProcessLowStockAlertsAsync(today));

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
			DeduplicationKey: null));
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
			ReminderDate: issue.IssueDate.Date,
			DeduplicationKey: null));
	}

	public async Task<List<LibraryNotificationHistory>> SendReturnNotificationsAsync(BookIssue issue, Book? book, BookCopy? copy)
	{
		var recipient = await recipientResolver.ResolveMemberAsync(issue.MemberId, issue.MemberRole);
		var placeholders = BuildIssuePlaceholders(issue, book, copy);
		placeholders["returnDate"] = FormatDate(issue.ReturnDate);
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
			ReminderDate: issue.ReturnDate?.Date,
			DeduplicationKey: null));

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
				ReminderDate: issue.FineDueDate?.Date ?? issue.ReturnDate?.Date,
				DeduplicationKey: null)));
		}

		return histories;
	}

	public async Task<List<LibraryNotificationHistory>> SendReservationConfirmedAsync(BookReservation reservation, Book book)
	{
		var recipient = await recipientResolver.ResolveMemberAsync(reservation.MemberId);
		return await deliveryService.DeliverAsync(new LibraryNotificationDeliveryRequest(
			LibraryNotificationEventTypes.ReservationConfirmed,
			RealTimeChannels(),
			new[] { recipient },
			BuildReservationPlaceholders(reservation, book),
			SubjectOverride: null,
			MessageOverride: null,
			RelatedEntityType: nameof(BookReservation),
			RelatedEntityId: reservation.Id,
			ReminderDate: reservation.RequestedDate.Date,
			DeduplicationKey: null));
	}

	private async Task<List<LibraryNotificationHistory>> ProcessDueIssueRemindersAsync(DateTime today, int offsetDays)
	{
		var targetDueDate = today.AddDays(offsetDays);
		var issues = await GetActiveIssuesAsync(issue => issue.DueDate.Date == targetDueDate);
		return await SendIssueReminderBatchAsync(issues, LibraryNotificationEventTypes.DueDateReminder, today);
	}

	private async Task<List<LibraryNotificationHistory>> ProcessOverdueIssueRemindersAsync(DateTime today)
	{
		var issues = await GetActiveIssuesAsync(issue => issue.DueDate.Date < today);
		return await SendIssueReminderBatchAsync(issues, LibraryNotificationEventTypes.OverdueReminder, today);
	}

	private async Task<List<LibraryNotificationHistory>> ProcessFineDueRemindersAsync(DateTime today)
	{
		var chargeQuery = await chargeRepository.GetQueryableAsync();
		var charges = await asyncExecuter.ToListAsync(chargeQuery.Where(x =>
			x.PaymentStatus == ChargePaymentStatus.Pending &&
			x.DueDate.Date <= today));

		var histories = new List<LibraryNotificationHistory>();
		foreach (var charge in charges)
		{
			var recipient = await recipientResolver.ResolveMemberAsync(charge.MemberId, charge.MemberRole);
			var book = await ResolveBookForChargeAsync(charge);
			histories.AddRange(await deliveryService.DeliverAsync(new LibraryNotificationDeliveryRequest(
				LibraryNotificationEventTypes.FineDueReminder,
				RealTimeChannels(),
				new[] { recipient },
				new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
				{
					["memberName"] = recipient.RecipientName,
					["bookTitle"] = book?.Title ?? "Library item",
					["amount"] = FormatAmount(charge.Amount),
					["fineDueDate"] = FormatDate(charge.DueDate)
				},
				SubjectOverride: null,
				MessageOverride: null,
				RelatedEntityType: nameof(LibraryCharge),
				RelatedEntityId: charge.Id,
				ReminderDate: today,
				DeduplicationKey: null)));
		}

		return histories;
	}

	private async Task<List<LibraryNotificationHistory>> ProcessReservationAvailabilityRemindersAsync(DateTime today)
	{
		var reservations = await GetActiveReservationsAsync(today);
		var histories = new List<LibraryNotificationHistory>();

		foreach (var reservation in reservations)
		{
			var book = await bookRepository.FindAsync(reservation.BookId);
			if (book == null || !await HasAvailableCopyAsync(book.Id))
			{
				continue;
			}

			var recipient = await recipientResolver.ResolveMemberAsync(reservation.MemberId);
			histories.AddRange(await deliveryService.DeliverAsync(new LibraryNotificationDeliveryRequest(
				LibraryNotificationEventTypes.ReservedBookAvailable,
				RealTimeChannels(),
				new[] { recipient },
				BuildReservationPlaceholders(reservation, book),
				SubjectOverride: null,
				MessageOverride: null,
				RelatedEntityType: nameof(BookReservation),
				RelatedEntityId: reservation.Id,
				ReminderDate: today,
				DeduplicationKey: null)));
		}

		return histories;
	}

	private async Task<List<LibraryNotificationHistory>> ProcessReservationExpiringRemindersAsync(DateTime today, int offsetDays)
	{
		var targetDate = today.AddDays(offsetDays);
		var reservations = (await GetActiveReservationsAsync(today))
			.Where(x => x.ReserveDueDate.Date <= targetDate)
			.ToList();
		var histories = new List<LibraryNotificationHistory>();

		foreach (var reservation in reservations)
		{
			var book = await bookRepository.FindAsync(reservation.BookId);
			if (book == null)
			{
				continue;
			}

			var recipient = await recipientResolver.ResolveMemberAsync(reservation.MemberId);
			histories.AddRange(await deliveryService.DeliverAsync(new LibraryNotificationDeliveryRequest(
				LibraryNotificationEventTypes.ReservationExpiring,
				RealTimeChannels(),
				new[] { recipient },
				BuildReservationPlaceholders(reservation, book),
				SubjectOverride: null,
				MessageOverride: null,
				RelatedEntityType: nameof(BookReservation),
				RelatedEntityId: reservation.Id,
				ReminderDate: today,
				DeduplicationKey: null)));
		}

		return histories;
	}

	private async Task<List<LibraryNotificationHistory>> ProcessLowStockAlertsAsync(DateTime today)
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
				DeduplicationKey: null)));
		}

		return histories;
	}

	private async Task<List<LibraryNotificationHistory>> SendIssueReminderBatchAsync(
		IReadOnlyCollection<BookIssue> issues,
		string eventType,
		DateTime today)
	{
		var histories = new List<LibraryNotificationHistory>();
		foreach (var issue in issues)
		{
			var copy = await copyRepository.FindAsync(issue.BookCopyId);
			var book = copy == null ? null : await bookRepository.FindAsync(copy.BookId);
			var recipient = await recipientResolver.ResolveMemberAsync(issue.MemberId, issue.MemberRole);

			histories.AddRange(await deliveryService.DeliverAsync(new LibraryNotificationDeliveryRequest(
				eventType,
				RealTimeChannels(),
				new[] { recipient },
				BuildIssuePlaceholders(issue, book, copy),
				SubjectOverride: null,
				MessageOverride: null,
				RelatedEntityType: nameof(BookIssue),
				RelatedEntityId: issue.Id,
				ReminderDate: today,
				DeduplicationKey: null)));
		}

		return histories;
	}

	private async Task<List<BookIssue>> GetActiveIssuesAsync(Func<BookIssue, bool> predicate)
	{
		var query = await issueRepository.GetQueryableAsync();
		var issues = await asyncExecuter.ToListAsync(query.Where(x => x.Status == BookIssueStatus.Issued));
		return issues.Where(predicate).ToList();
	}

	private async Task<List<BookReservation>> GetActiveReservationsAsync(DateTime today)
	{
		var query = await reservationRepository.GetQueryableAsync();
		return await asyncExecuter.ToListAsync(query.Where(x => x.ReserveDueDate.Date >= today));
	}

	private async Task<bool> HasAvailableCopyAsync(Guid bookId)
	{
		var query = await copyRepository.GetQueryableAsync();
		return await asyncExecuter.AnyAsync(query.Where(x => x.BookId == bookId && x.Status == BookCopyStatus.Available));
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
			["issueDate"] = FormatDate(issue.IssueDate),
			["dueDate"] = FormatDate(issue.DueDate)
		};

	private static Dictionary<string, string?> BuildReservationPlaceholders(BookReservation reservation, Book book) =>
		new(StringComparer.OrdinalIgnoreCase)
		{
			["bookTitle"] = book.Title,
			["requestedDate"] = FormatDate(reservation.RequestedDate),
			["reserveDueDate"] = FormatDate(reservation.ReserveDueDate)
		};

	private static IReadOnlyCollection<string> RealTimeChannels() =>
		new[] { LibraryNotificationChannels.InApp, LibraryNotificationChannels.Email };

	private static int GetReminderOffset(IEnumerable<LibraryNotificationSetting> settings, string eventType, int fallback)
	{
		return settings
			.Where(x => x.EventType.Equals(eventType, StringComparison.OrdinalIgnoreCase) && x.IsActive)
			.Select(x => x.ReminderOffsetDays)
			.FirstOrDefault(x => x.HasValue)
			?? fallback;
	}

	private static string FormatDate(DateTime? date) =>
		date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;

	private static string FormatAmount(decimal? amount) =>
		amount?.ToString("0.##", CultureInfo.InvariantCulture) ?? "0";
}

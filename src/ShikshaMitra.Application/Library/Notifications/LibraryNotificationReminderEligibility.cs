using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Members;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Notifications;

public interface ILibraryNotificationReminderEligibility
{
	Task<bool> CaptureAsync(LibraryNotificationHistory row, LibraryFineReminderEvidence? expectedFine = null);
	Task<bool> IsCurrentAsync(LibraryNotificationHistory row);
	Task<bool> AllowsRecoveryAsync(LibraryNotificationHistory row);
	Task<LibraryInboxRecoveryScope> GetInboxRecoveryScopeAsync(IReadOnlyCollection<Guid> memberIds, IReadOnlyCollection<Guid> fineSources);
}

public sealed record LibraryInboxRecoveryScope(Guid[] IssueReceiptIds, Guid[] OpenIssueIds, Guid[] ReturnedIssueIds, Guid[] CollectibleChargeIds, Guid[] ReturnChargeIssueIds);

public class LibraryNotificationReminderEligibility(
	IRepository<BookIssue, Guid> issues,
	IRepository<LibraryCharge, Guid> charges,
	IRepository<LibraryMember, Guid> memberships,
	IRepository<Book, Guid> books,
	IRepository<BookCopy, Guid> copies,
	LibraryNotificationPreferences preferences,
	LibraryReservationNotificationEvidenceReader reservationEvidence,
	ILibraryFineReminderEvidenceReader fineEvidence,
	ICurrentTenant tenant) : ILibraryNotificationReminderEligibility, ITransientDependency
{
	private const string StateKey = "RelatedLibraryStateSignature";

	public async Task<bool> CaptureAsync(LibraryNotificationHistory row, LibraryFineReminderEvidence? expectedFine = null)
	{
		if (!LibraryNotificationSource.IsConsistent(row.EventType, row.RelatedEntityType, row.RelatedEntityId)) return false;
		if (IsLinkedFine(row) && expectedFine == null) return false;
		var state = await ReadStateAsync(row, expectedFine);
		if (state != null) row.ExtraProperties[StateKey] = Signature(state);
		return state != null || !row.RelatedEntityId.HasValue || !TracksState(row.EventType);
	}

	public async Task<bool> IsCurrentAsync(LibraryNotificationHistory row)
	{
		if (!LibraryNotificationSource.IsConsistent(row.EventType, row.RelatedEntityType, row.RelatedEntityId)) return false;
		if (!row.RelatedEntityId.HasValue || !TracksState(row.EventType)) return true;
		var state = await ReadStateAsync(row);
		return state != null && row.ExtraProperties.TryGetValue(StateKey, out var captured) && captured?.ToString() == Signature(state);
	}

	public async Task<bool> AllowsRecoveryAsync(LibraryNotificationHistory row) =>
		row.RecipientMemberId.HasValue && IsRecoveryPurpose(row.EventType) &&
		(IsLinkedFine(row) ? await IsCurrentAsync(row) : await ReadStateAsync(row) != null);

	public async Task<LibraryInboxRecoveryScope> GetInboxRecoveryScopeAsync(IReadOnlyCollection<Guid> memberIds, IReadOnlyCollection<Guid> fineSources)
	{
		if (memberIds.Count == 0) return new([], [], [], [], []);
		var canonicalIds = memberIds.ToArray();
		var aliases = await memberships.GetListAsync(member => member.TenantId == tenant.Id && Enumerable.Contains(canonicalIds, member.StudentId));
		var personIds = canonicalIds.Concat(aliases.Select(member => member.Id)).Distinct().ToArray();
		var ownedIssues = await issues.GetListAsync(issue => issue.TenantId == tenant.Id && Enumerable.Contains(personIds, issue.MemberId));
		var pending = await charges.GetListAsync(charge => charge.TenantId == tenant.Id && Enumerable.Contains(personIds, charge.MemberId) &&
			charge.PaymentStatus == ChargePaymentStatus.Pending && charge.FinanceSourceLinkId == null);
		var collectible = new List<Guid>();
		foreach (var page in fineSources.Chunk(LibraryFineReminderEvidenceReader.BatchSize))
		{
			// These notices were already delivered. Recovery requires a continuing verified balance.
			var positions = await fineEvidence.ReadAsync(page, DateOnly.MaxValue);
			collectible.AddRange(positions.Values.Where(position => memberIds.Contains(position.PayerId)).Select(position => position.AssessmentId));
		}
		var issueIds = ownedIssues.Select(issue => issue.Id).ToArray();
		return new(issueIds,
			ownedIssues.Where(issue => issue.Status == BookIssueStatus.Issued).Select(issue => issue.Id).ToArray(),
			ownedIssues.Where(issue => issue.Status == BookIssueStatus.Returned).Select(issue => issue.Id).ToArray(),
			collectible.ToArray(),
			pending.Where(charge => charge.BookIssueId.HasValue).Select(charge => charge.BookIssueId!.Value).Intersect(issueIds).Distinct().ToArray());
	}

	private async Task<RelatedState?> ReadStateAsync(LibraryNotificationHistory row, LibraryFineReminderEvidence? expectedFine = null)
	{
		if (!row.RelatedEntityId.HasValue || row.TenantId != tenant.Id ||
			!LibraryNotificationSource.IsConsistent(row.EventType, row.RelatedEntityType, row.RelatedEntityId)) return null;
		if (row.EventType is LibraryNotificationEventTypes.BookIssueConfirmation or LibraryNotificationEventTypes.DueDateReminder or
			LibraryNotificationEventTypes.OverdueReminder or LibraryNotificationEventTypes.ReturnConfirmation or LibraryNotificationEventTypes.ReturnChargeReminder)
		{
			var issue = await issues.FindAsync(row.RelatedEntityId.Value);
			if (issue == null || issue.TenantId != tenant.Id || !await BelongsAsync(issue.MemberId, row.RecipientMemberId)) return null;
			if (row.EventType == LibraryNotificationEventTypes.ReturnConfirmation)
				return issue.Status == BookIssueStatus.Returned ? State(issue) : null;
			if (row.EventType == LibraryNotificationEventTypes.ReturnChargeReminder)
			{
				var pending = await charges.GetListAsync(charge => charge.TenantId == tenant.Id && charge.BookIssueId == issue.Id &&
					charge.PaymentStatus == ChargePaymentStatus.Pending && charge.FinanceSourceLinkId == null);
				var amount = 0m;
				foreach (var charge in pending)
					if (await BelongsAsync(charge.MemberId, row.RecipientMemberId)) amount += charge.Amount;
				return amount > 0 ? State(issue) with { Amount = amount } : null;
			}
			if (row.EventType is LibraryNotificationEventTypes.DueDateReminder or LibraryNotificationEventTypes.OverdueReminder)
			{
				var today = await SchoolDateAsync(row);
				var due = LibraryLoanDates.ConfirmedDueDate(issue);
				if (!today.HasValue || !due.HasValue ||
					(row.EventType == LibraryNotificationEventTypes.DueDateReminder && due.Value < DateOnly.FromDateTime(today.Value)) ||
					(row.EventType == LibraryNotificationEventTypes.OverdueReminder && due.Value >= DateOnly.FromDateTime(today.Value))) return null;
			}
			return issue.Status == BookIssueStatus.Issued ? State(issue) : null;
		}
		if (row.EventType == LibraryNotificationEventTypes.FineDueReminder) return await ReadFineStateAsync(row, expectedFine);
		if (row.EventType is LibraryNotificationEventTypes.ReservationConfirmed or LibraryNotificationEventTypes.ReservedBookAvailable or LibraryNotificationEventTypes.ReservationExpiring)
		{
			var today = row.EventType == LibraryNotificationEventTypes.ReservationConfirmed ? null : await SchoolDateAsync(row);
			if (row.EventType != LibraryNotificationEventTypes.ReservationConfirmed && !today.HasValue) return null;
			var evidence = await reservationEvidence.ReadAsync(row.RelatedEntityId.Value, row.EventType,
				today.HasValue ? DateOnly.FromDateTime(today.Value) : null);
			if (evidence == null || evidence.MemberId != row.RecipientMemberId) return null;
			return new RelatedState("Reservation", evidence.Request.Id, evidence.MemberId, evidence.Signature,
				evidence.Deadline?.ToDateTime(TimeOnly.MinValue), null, null, evidence.Hold?.CopyId ?? evidence.Book.Id);
		}
		if (row.EventType == LibraryNotificationEventTypes.LowStockAlert)
		{
			var book = await books.FindAsync(row.RelatedEntityId.Value);
			if (book == null || book.TenantId != tenant.Id) return null;
			var available = await copies.CountAsync(copy => copy.TenantId == tenant.Id && copy.BookId == book.Id && copy.Status == BookCopyStatus.Available);
			return available <= 2 ? new RelatedState("Stock", book.Id, Guid.Empty, "LowStock", null, available, null, null) : null;
		}
		return null;
	}

	private async Task<RelatedState?> ReadFineStateAsync(LibraryNotificationHistory row, LibraryFineReminderEvidence? expected = null)
	{
		if (!tenant.Id.HasValue || row.TenantId != tenant.Id || row.RelatedEntityId is not Guid id || row.ReminderDate is not DateTime runDate)
			return null;
		var positions = await fineEvidence.ReadAsync([id], DateOnly.FromDateTime(runDate));
		if (!positions.TryGetValue(id, out var current) || current.PayerId != row.RecipientMemberId ||
			(expected != null && current != expected)) return null;
		var destination = row.ExtraProperties.GetValueOrDefault("RecipientKind")?.ToString();
		var typedRecipient = current.PayerKind == FinanceExternalPayerKind.Employee
			? destination == "Employee"
			: destination is "LegacySchoolCommunication" or "SavedGuardianContact" or "VerifiedStudent" or "VerifiedGuardian";
		return typedRecipient ? new RelatedState("FinanceFine", id, current.PayerId, $"{current.PayerKind}:{current.Currency}",
			current.DueDate.ToDateTime(TimeOnly.MinValue), current.Collectible, null, current.SourceLinkId) : null;
	}

	private static bool IsLinkedFine(LibraryNotificationHistory row) =>
		row.EventType == LibraryNotificationEventTypes.FineDueReminder && row.RelatedEntityId.HasValue;

	private async Task<bool> BelongsAsync(Guid recordedMemberId, Guid? recipientId) => recipientId.HasValue &&
		(recordedMemberId == recipientId.Value || await memberships.AnyAsync(member => member.TenantId == tenant.Id &&
			member.Id == recordedMemberId && member.StudentId == recipientId.Value));

	private async Task<DateTime?> SchoolDateAsync(LibraryNotificationHistory row)
	{
		try { return await preferences.GetSchoolDateAsync(); }
		catch (BusinessException error) when (error.Code is LibraryErrorCodes.CalendarNotConfigured or LibraryErrorCodes.CalendarInvalid)
		{
			return row.ReminderDate?.Date;
		}
	}

	private static RelatedState State(BookIssue issue) => new("Issue", issue.Id, issue.MemberId, issue.Status.ToString(),
		LibraryLoanDates.ConfirmedDueDate(issue)?.ToDateTime(TimeOnly.MinValue), issue.Amount,
		LibraryLoanDates.ConfirmedReturnDate(issue)?.ToDateTime(TimeOnly.MinValue), issue.BookCopyId,
		LibraryLoanDates.ConfirmedIssueDate(issue)?.ToDateTime(TimeOnly.MinValue));
	private static string Signature(RelatedState state) => "v3:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
	{
		Version = 3, state.Kind, state.Id, state.MemberId, state.Status,
		DueDate = CalendarDate(state.DueDate), Amount = state.Amount?.ToString("G29", CultureInfo.InvariantCulture),
		ReturnDate = CalendarDate(state.ReturnDate), IssueDate = CalendarDate(state.IssueDate), state.ItemId
	}))));

	private static string? CalendarDate(DateTime? value) => value.HasValue
		? DateOnly.FromDateTime(value.Value).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null;
	private static bool IsRecoveryPurpose(string eventType) => eventType is LibraryNotificationEventTypes.BookIssueConfirmation or
		LibraryNotificationEventTypes.DueDateReminder or LibraryNotificationEventTypes.OverdueReminder or LibraryNotificationEventTypes.ReturnConfirmation or
		LibraryNotificationEventTypes.ReturnChargeReminder or LibraryNotificationEventTypes.FineDueReminder;
	private static bool TracksState(string eventType) => IsRecoveryPurpose(eventType) || eventType is LibraryNotificationEventTypes.ReservationConfirmed or
		LibraryNotificationEventTypes.ReservedBookAvailable or LibraryNotificationEventTypes.ReservationExpiring or LibraryNotificationEventTypes.LowStockAlert;
	private sealed record RelatedState(string Kind, Guid Id, Guid MemberId, string Status, DateTime? DueDate, decimal? Amount, DateTime? ReturnDate, Guid? ItemId, DateTime? IssueDate = null);
}

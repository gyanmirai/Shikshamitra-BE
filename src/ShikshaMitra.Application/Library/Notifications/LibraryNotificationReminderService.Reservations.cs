using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Reservations;

namespace ShikshaMitra.Library.Notifications;

public partial class LibraryNotificationReminderService
{
	public async Task<List<LibraryNotificationHistory>> SendReservationConfirmedAsync(BookReservation request, Book book)
	{
		var evidence = await reservationEvidence.ReadAsync(request.Id, LibraryNotificationEventTypes.ReservationConfirmed, null);
		var origin = authority.ForCurrentUser(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Reserve);
		if (evidence == null || evidence.Book.Id != book.Id)
			return await deliveryService.RecordReservationSkippedAsync(request.Id, request.CanonicalMemberId,
				"This reservation requires confirmed member and school-date review before notification.", request.Revision, origin,
				LibraryNotificationEventTypes.ReservationConfirmed);
		return await SendReservationEvidenceAsync(evidence, LibraryNotificationEventTypes.ReservationConfirmed,
			evidence.Request.SchoolRequestedDate!.Value.ToDateTime(TimeOnly.MinValue), origin);
	}

	private async Task<List<LibraryNotificationHistory>> ProcessReservationAvailabilityRemindersAsync(DateTime today,
		IReadOnlySet<Guid>? allowedMemberIds, LibraryNotificationOrigin origin)
	{
		var histories = new List<LibraryNotificationHistory>();
		foreach (var request in await GetConfirmedReservationsAsync(allowedMemberIds, origin))
		{
			var evidence = await reservationEvidence.ReadAsync(request.Id, LibraryNotificationEventTypes.ReservedBookAvailable, DateOnly.FromDateTime(today));
			if (evidence != null) histories.AddRange(await SendReservationEvidenceAsync(evidence,
				LibraryNotificationEventTypes.ReservedBookAvailable, today, origin));
		}
		return histories;
	}

	private async Task<List<LibraryNotificationHistory>> ProcessReservationExpiringRemindersAsync(DateTime today,
		List<LibraryNotificationSetting> settings, IReadOnlySet<Guid>? allowedMemberIds, LibraryNotificationOrigin origin)
	{
		var histories = new List<LibraryNotificationHistory>();
		var requests = await GetConfirmedReservationsAsync(allowedMemberIds, origin);
		foreach (var group in OffsetGroups(settings, LibraryNotificationEventTypes.ReservationExpiring, 1))
		{
			var target = DateOnly.FromDateTime(today.AddDays(group.Key));
			foreach (var request in requests)
			{
				var evidence = await reservationEvidence.ReadAsync(request.Id, LibraryNotificationEventTypes.ReservationExpiring, DateOnly.FromDateTime(today));
				if (evidence?.Deadline == null || evidence.Deadline.Value > target) continue;
				histories.AddRange(await SendReservationEvidenceAsync(evidence, LibraryNotificationEventTypes.ReservationExpiring,
					today, origin, group.Select(setting => setting.Channel).ToArray()));
			}
		}
		return histories;
	}

	private async Task<List<BookReservation>> GetConfirmedReservationsAsync(IReadOnlySet<Guid>? allowedMemberIds, LibraryNotificationOrigin origin)
	{
		var query = (await candidates.GetRequestsAsync(origin)).Where(row => row.TenantId == currentTenant.Id &&
			row.CanonicalMemberId.HasValue && (row.State == LibraryReservationState.Waiting || row.State == LibraryReservationState.Ready));
		if (origin.IsSystem && allowedMemberIds != null)
		{
			var ids = allowedMemberIds.ToArray();
			query = query.Where(row => Enumerable.Contains(ids, row.CanonicalMemberId!.Value));
		}
		return await asyncExecuter.ToListAsync(query);
	}

	private async Task<List<LibraryNotificationHistory>> SendReservationEvidenceAsync(LibraryReservationNotificationEvidence evidence,
		string eventType, DateTime reminderDate, LibraryNotificationOrigin origin, IReadOnlyCollection<string>? channels = null)
	{
		var recipient = await recipientResolver.ResolveMemberAsync(evidence.MemberId, evidence.Request.MemberRole);
		return await deliveryService.DeliverAsync(new LibraryNotificationDeliveryRequest(eventType, channels ?? RealTimeChannels(),
			[recipient], evidence.Placeholders(), null, null, nameof(BookReservation), evidence.Request.Id, reminderDate, null, origin));
	}
}

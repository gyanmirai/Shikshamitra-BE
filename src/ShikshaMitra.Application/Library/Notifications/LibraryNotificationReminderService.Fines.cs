using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Books;

namespace ShikshaMitra.Library.Notifications;

public partial class LibraryNotificationReminderService
{
	private async Task<List<LibraryNotificationHistory>> ProcessFineDueRemindersAsync(DateTime today, LibraryNotificationOrigin origin)
	{
		var schoolDate = DateOnly.FromDateTime(today);
		var candidates = await fineEvidence.GetCandidatesAsync(schoolDate, origin);
		var histories = new List<LibraryNotificationHistory>();
		Guid? after = null;
		while (true)
		{
			var page = after is Guid last ? candidates.Where(row => row.Id.CompareTo(last) > 0) : candidates;
			var charges = await asyncExecuter.ToListAsync(page.OrderBy(row => row.Id).Take(LibraryFineReminderEvidenceReader.BatchSize));
			if (charges.Count == 0) return histories;
			var amounts = await fineEvidence.ReadAsync(charges.Select(row => row.Id).ToArray(), schoolDate);
			foreach (var charge in charges)
			{
				if (!amounts.TryGetValue(charge.Id, out var evidence)) continue;
				var role = evidence.PayerKind == FinanceExternalPayerKind.Student ? 0 : 1;
				var recipient = await recipientResolver.ResolveMemberAsync(evidence.PayerId, role);
				var book = await ResolveBookForChargeAsync(charge);
				histories.AddRange(await deliveryService.DeliverAsync(new LibraryNotificationDeliveryRequest(
					LibraryNotificationEventTypes.FineDueReminder, RealTimeChannels(), [recipient],
					new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
					{
						["memberName"] = recipient.RecipientName,
						["bookTitle"] = book?.Title ?? "Library item",
						["amount"] = $"{evidence.Currency} {evidence.Collectible.ToString("F2", CultureInfo.InvariantCulture)}",
						["fineDueDate"] = FormatSchoolDate(evidence.DueDate)
					},
					SubjectOverride: null, MessageOverride: null, RelatedEntityType: nameof(LibraryCharge), RelatedEntityId: charge.Id,
					ReminderDate: today, DeduplicationKey: null, Origin: origin, FineEvidence: evidence)));
			}
			if (charges.Count < LibraryFineReminderEvidenceReader.BatchSize) return histories;
			after = charges[^1].Id;
		}
	}
}

using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Reservations;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Library.Notifications;

public interface ILibraryReservationNotificationSink
{
	Task NotifyAllocatedAsync(BookReservation request, LibraryHold hold, Book book, BookCopy copy);
}

public class LibraryReservationNotificationSink(LibraryReservationNotificationEvidenceReader evidenceReader,
	LibraryNotificationPreferences preferences, LibraryNotificationRecipientResolver recipients,
	LibraryNotificationDeliveryService delivery) : ILibraryReservationNotificationSink, ITransientDependency
{
	public async Task NotifyAllocatedAsync(BookReservation request, LibraryHold hold, Book book, BookCopy copy)
	{
		var evidence = await evidenceReader.ReadAsync(request.Id, LibraryNotificationEventTypes.ReservedBookAvailable, null);
		if (evidence == null || evidence.Hold?.Id != hold.Id || evidence.Copy?.Id != copy.Id || evidence.Book.Id != book.Id) return;
		var setup = await preferences.GetSetupAsync();
		if (setup.NeedsConfiguration)
		{
			await delivery.RecordReservationSkippedAsync(request.Id, evidence.MemberId, string.Join(" ", setup.ConfigurationIssues),
				evidence.Signature, LibraryNotificationOrigin.HoldAllocated);
			return;
		}
		var today = await preferences.GetSchoolDateAsync();
		if (evidence.Deadline.HasValue && evidence.Deadline.Value < DateOnly.FromDateTime(today)) return;
		var recipient = await recipients.ResolveMemberAsync(evidence.MemberId, request.MemberRole);
		await delivery.DeliverAsync(new LibraryNotificationDeliveryRequest(LibraryNotificationEventTypes.ReservedBookAvailable,
			[LibraryNotificationChannels.InApp, LibraryNotificationChannels.Email], [recipient], evidence.Placeholders(), null, null,
			nameof(BookReservation), request.Id, today, null, LibraryNotificationOrigin.HoldAllocated));
	}
}

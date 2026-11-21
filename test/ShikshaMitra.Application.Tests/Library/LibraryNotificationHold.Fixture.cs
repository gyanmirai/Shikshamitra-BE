using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Notifications;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Students;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library;

public partial class LibraryNotificationRecipientAccess_Tests
{
	private Task<NotificationHoldContext> SeedNotificationHoldAsync(Context context, bool ready = true, bool spare = false,
		BookCopyStatus copyStatus = BookCopyStatus.Reserved, bool openLoan = false, Guid? memberId = null, bool reviewed = false) => WithUnitOfWorkAsync(async () => {
		var copies = GetRequiredService<IRepository<BookCopy, Guid>>();
		var copy = await copies.GetAsync(context.CopyId); copy.Status = copyStatus; await copies.UpdateAsync(copy, true);
		var student = await GetRequiredService<IRepository<Student, Guid>>().GetAsync(context.StudentId);
		var person = memberId ?? context.StudentId;
		var request = reviewed ? new BookReservation(Guid.NewGuid(), copy.BookId, context.MemberId,
			new DateTime(2026, 11, 16), new DateTime(2026, 11, 30), context.TenantId) : BookReservation.Create(Guid.NewGuid(), context.TenantId, copy.BookId, person,
			person == context.StudentId ? context.MemberId : person, 0, student.AcademicYearId!.Value, 1,
			new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 30), "{}", new DateTime(2026, 11, 16), new DateTime(2026, 11, 30));
		if (reviewed) request.Review(person, 0, student.AcademicYearId!.Value, 1, new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 30), "{}");
		if (ready) request.Ready();
		await GetRequiredService<IRepository<BookReservation, Guid>>().InsertAsync(request, true);
		Guid? holdId = null;
		if (ready) {
			var hold = new LibraryHold(Guid.NewGuid(), context.TenantId, request.Id, copy.BookId, copy.Id,
				new DateTimeOffset(2026, 11, 17, 3, 0, 0, TimeSpan.Zero), new DateOnly(2026, 11, 18),
				LibraryCivilDateProvenance.ExplicitSchoolDate, "Asia/Kolkata", "{}", "{}");
			await GetRequiredService<IRepository<LibraryHold, Guid>>().InsertAsync(hold, true); holdId = hold.Id;
		}
		Guid? spareId = null;
		if (spare) {
			var extra = new BookCopy(Guid.NewGuid(), copy.BookId, 2, "SPARE-" + Guid.NewGuid().ToString("N"), copy.SectionId, copy.ShelfId, context.TenantId);
			await copies.InsertAsync(extra, true); spareId = extra.Id;
		}
		if (openLoan) await GetRequiredService<IRepository<BookIssue, Guid>>().InsertAsync(new BookIssue(Guid.NewGuid(), copy.Id,
			copy.AccessionNumber, person, 0, new DateTime(2026, 11, 10), new DateTime(2026, 11, 20), context.TenantId), true);
		return new NotificationHoldContext(request.Id, holdId, copy.BookId, copy.Id, spareId);
	});
	private static LibraryNotificationHistory ReservationIntent(Context context, NotificationHoldContext hold) => new(Guid.NewGuid(), context.TenantId,
		LibraryNotificationEventTypes.ReservedBookAvailable, LibraryNotificationEventGroups.BookManagement, "Reserved book available",
		LibraryNotificationChannels.Email, LibraryNotificationRecipientRoles.Member, context.StudentId, "Library reader", "guardian@example.test",
		"Collect your held copy", "A copy is held for you.", LibraryNotificationDeliveryStatuses.Queued, null, nameof(BookReservation),
		hold.RequestId, null, new DateTime(2026, 11, 17), null);
	private sealed record NotificationHoldContext(Guid RequestId, Guid? HoldId, Guid BookId, Guid CopyId, Guid? SpareId);
}

using System;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Notifications;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using Shouldly;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library;

public partial class LibraryNotificationRecipientAccess_Tests
{
	private Task<LibraryNotificationHistory> SeedPriorYearNotificationAsync(Context context, bool reservation) => WithUnitOfWorkAsync(async () => {
		var year = new AcademicYear(Guid.NewGuid(), context.TenantId, "Previous Library year", new DateTime(2025, 4, 1), new DateTime(2026, 3, 31));
		await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(year, true);
		var copy = await GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(context.CopyId);
		Guid recordId;
		if (reservation)
		{
			var request = BookReservation.Create(Guid.NewGuid(), context.TenantId, copy.BookId, context.StudentId, context.MemberId,
				0, year.Id, 1, new DateOnly(2025, 11, 10), new DateOnly(2025, 11, 18), "{}");
			await GetRequiredService<IRepository<BookReservation, Guid>>().InsertAsync(request, true);
			recordId = request.Id;
		}
		else
		{
			var loan = BookIssue.CreateCivil(Guid.NewGuid(), context.TenantId, copy.Id, copy.AccessionNumber, context.StudentId, 0,
				new DateOnly(2025, 11, 10), new DateOnly(2025, 11, 18), new LibraryLoanIssueContext(year.Id, null, null, null, null, null,
					"{}", "{}", "{}", context.ActorId, new DateTimeOffset(2025, 11, 10, 3, 0, 0, TimeSpan.Zero)));
			await GetRequiredService<IRepository<BookIssue, Guid>>().InsertAsync(loan, true);
			recordId = loan.Id;
		}
		var eventType = reservation ? LibraryNotificationEventTypes.ReservationExpiring : LibraryNotificationEventTypes.OverdueReminder;
		var row = new LibraryNotificationHistory(Guid.NewGuid(), context.TenantId, eventType,
			LibraryNotificationEventGroups.Transactions, "Prior-year notification", LibraryNotificationChannels.Email,
			LibraryNotificationRecipientRoles.Member, context.StudentId, "Library reader", "guardian@example.test", "Recorded notification",
			"A prior-year Library record.", LibraryNotificationDeliveryStatuses.Failed, "Delivery failed", reservation ? nameof(BookReservation) : nameof(BookIssue),
			recordId, null, null, null);
		var authority = Services().Get<LibraryNotificationAuthorityService>();
		authority.Capture(row, authority.ForCurrentUser(Send));
		await GetRequiredService<IRepository<LibraryNotificationHistory, Guid>>().InsertAsync(row, true);
		return row;
	});

	private async Task LimitNotificationActorToCurrentYearAsync(Context context)
	{
		await WithUnitOfWorkAsync(async () => {
			var student = await GetRequiredService<IRepository<Student, Guid>>().GetAsync(context.StudentId);
			var assignments = GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
			await assignments.DeleteAsync(row => row.UserId == context.ActorId, true);
			await assignments.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), context.TenantId, userId: context.ActorId,
				academicYearId: student.AcademicYearId), true);
			await GetRequiredService<IUserScopeService>().InvalidateUserScopeCacheAsync(context.ActorId);
		});
		(await WithUnitOfWorkAsync(() => GetRequiredService<ILibraryAccessService>().GetMemberIdsAsync())).ShouldContain(context.StudentId);
	}
}

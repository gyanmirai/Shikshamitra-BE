using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Notifications;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Shared;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryNotificationRecipientAccess_Tests
{
	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task Notification_Recorded_Class_Should_Authorize_New_Request_History_By_Its_Original_Class_After_Promotion(bool originalClass)
	{
		var context = await SeedAsync(); using var school = GetRequiredService<ICurrentTenant>().Change(context.TenantId);
		using var actor = Principal(context.ActorId);
		var placement = await SeedNotificationClassesAsync(context);
		await WithUnitOfWorkAsync(async () => {
			var grants = GetRequiredService<IPermissionManager>();
			await grants.SetAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.View, "U", context.ActorId.ToString(), true);
			await grants.SetAsync(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Reserve, "U", context.ActorId.ToString(), true);
		});
		await AssignNotificationClassAsync(context, placement, placement.OriginalClassId);
		var requestId = await WithUnitOfWorkAsync(async () => {
			var copy = await GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(context.CopyId);
			var request = await GetRequiredService<LibraryReservationCoordinator>().CreateAsync(copy.BookId, context.StudentId,
				placement.YearId, new DateOnly(2026, 11, 17), new DateOnly(2026, 11, 30), LibraryOperationActor.User(context.ActorId));
			await Services().Get<LibraryNotificationReminderService>().SendReservationConfirmedAsync(request,
				await GetRequiredService<IRepository<Book, Guid>>().GetAsync(copy.BookId));
			return request.Id;
		});
		var bookId = await WithUnitOfWorkAsync(async () => (await GetRequiredService<IRepository<BookCopy, Guid>>().GetAsync(context.CopyId)).BookId);
		var beforeRequests = await WithUnitOfWorkAsync(() => GetRequiredService<LibraryReservationAppService>()
			.GetListAsync(bookId, placement.YearId, includeHistory: true));
		beforeRequests.Items.ShouldContain(row => row.Id == requestId);
		var input = new LibraryNotificationHistoryRequest { EventType = LibraryNotificationEventTypes.ReservationConfirmed, PageSize = 100 };
		var before = await WithUnitOfWorkAsync(() => Services().Get<LibraryNotificationAppService>().GetHistoryAsync(input));
		before.Items.ShouldContain(row => row.RelatedEntityId == requestId);
		before.TotalCount.ShouldBeGreaterThan(0);
		await MoveNotificationStudentAsync(context, placement, placement.NextClassId);
		if (!originalClass) await AssignNotificationClassAsync(context, placement, placement.NextClassId);
		var after = await WithUnitOfWorkAsync(() => Services().Get<LibraryNotificationAppService>().GetHistoryAsync(input));
		after.TotalCount.ShouldBe(originalClass ? before.TotalCount : 0);
		if (originalClass) after.Items.Select(row => row.Id).ShouldBe(before.Items.Select(row => row.Id));
		else after.Items.ShouldBeEmpty();
		var afterRequests = await WithUnitOfWorkAsync(() => GetRequiredService<LibraryReservationAppService>()
			.GetListAsync(bookId, placement.YearId, includeHistory: true));
		afterRequests.TotalCount.ShouldBe(originalClass ? beforeRequests.TotalCount : 0);
		if (originalClass) afterRequests.Items.ShouldContain(row => row.Id == requestId);
		else afterRequests.Items.ShouldBeEmpty();
	}
}

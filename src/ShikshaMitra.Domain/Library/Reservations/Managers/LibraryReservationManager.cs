using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;

namespace ShikshaMitra.Library.Reservations;

public class LibraryReservationManager(IRepository<LibraryReservationEvent, Guid> events, ILibraryInventoryLock inventoryLock,
	ICurrentTenant tenant, IAsyncQueryableExecuter queries, IClock clock) : DomainService
{
	public static string Capture(BookReservation request) => JsonSerializer.Serialize(new
	{
		request.Id, request.MemberId, request.CanonicalMemberId, request.OriginalMemberId, request.MemberRole,
		request.AcademicYearId, request.BorrowerBoardId, request.BorrowerMediumId, request.BorrowerStageId,
		request.BorrowerStandardId, request.BorrowerDivisionId, request.BorrowerScopeSnapshotJson, request.BorrowerScopeProvenance,
		request.BorrowerScopeCaptureId, request.BorrowerScopeCaptureVersion,
		request.QueueSequence, request.QueueProvenance, request.State,
		request.PickupSectionId, request.PickupCampusId, request.PickupLabel,
		request.RequestedDate, request.ReserveDueDate, request.SchoolRequestedDate, request.SchoolRequestExpiryDate,
		request.RequestedDateProvenance, request.RequestExpiryDateProvenance, request.PolicySnapshotJson,
		request.ReviewReason, request.TerminalReason, request.Revision
	});

	public async Task RecordAsync(BookReservation request, LibraryReservationState previous, string before,
		LibraryOperationActor actor, string reason, string eventType, LibraryHold? hold = null, Guid? loanId = null)
	{
		await inventoryLock.AcquireAsync(tenant.Id);
		if (request.TenantId != tenant.Id || hold != null && (hold.TenantId != tenant.Id || hold.ReservationId != request.Id || hold.BookId != request.BookId))
			throw new BusinessException(LibraryReservationErrorCodes.InvalidRequest);
		var last = await queries.FirstOrDefaultAsync((await events.GetQueryableAsync()).Where(row => row.ReservationId == request.Id)
			.OrderByDescending(row => row.Sequence).Select(row => row.Sequence));
		await events.InsertAsync(new LibraryReservationEvent(GuidGenerator.Create(), tenant.Id, request.Id, checked(last + 1),
			eventType, previous, request.State, actor, reason, new DateTimeOffset(clock.Now.ToUniversalTime()), before,
			Capture(request), hold?.Id, hold?.CopyId, loanId, LibraryBorrowerScopeCaptureEvidence.ReadCaptureId(before),
			request.BorrowerScopeCaptureId), autoSave: true);
	}
}

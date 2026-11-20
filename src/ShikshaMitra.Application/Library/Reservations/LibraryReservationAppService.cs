using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Notifications;
using ShikshaMitra.Library.Policies;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library.Reservations;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.Library.AllBooks.View)]
public partial class LibraryReservationAppService(IRepository<BookReservation, Guid> requests, IRepository<LibraryHold, Guid> holds,
	IRepository<LibraryReservationEvent, Guid> events, IRepository<Book, Guid> books, IRepository<BookCopy, Guid> copies,
	ILibraryAccessService access, LibraryReservationCoordinator coordinator, LibraryReservationEligibilityReader eligibility,
	IPermissionChecker permissions, LibraryNotificationReminderService notifications, LibraryReservationMemberReader memberReader,
	LibraryStoredReservationScope storedScope, LibraryReservationPickupReader pickupLocations) : ApplicationService, ILibraryReservationAppService
{
	public async Task<List<LibraryReservationPickupDto>> GetPickupLocationsAsync(Guid academicYearId)
	{
		await EnsurePermissionAsync(write: true);
		await access.GetContextAsync(academicYearId);
		return await pickupLocations.GetOptionsAsync(CurrentTenant.Id!.Value);
	}

	public async Task<LibraryReservationPreviewDto> PreviewAsync(LibraryReservationCreateRequest input)
	{
		await EnsurePermissionAsync(write: true);
		await access.GetContextAsync(input.AcademicYearId);
		await access.EnsureMemberAsync(input.MemberId);
		return await coordinator.PreviewAsync(input.BookId, input.MemberId, input.AcademicYearId, input.RequestedDate, input.RequestExpiryDate,
			pickupSectionId: input.PickupSectionId);
	}
	public async Task<LibraryReservationReviewPreviewDto> PreviewReviewAsync(Guid reservationId, LibraryReservationReviewPreviewRequest input)
	{
		await EnsurePermissionAsync(write: true);
		var request = await ReadAsync(reservationId);
		var context = await access.GetContextAsync(input.AcademicYearId);
		if (!context.UserScope.HasTenantWideScope()) throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		request.EnsureRevision(input.Revision);
		await access.EnsureMemberAsync(input.MemberId);
		var preview = await coordinator.PreviewAsync(request.BookId, input.MemberId, input.AcademicYearId,
			input.RequestedDate, input.RequestExpiryDate, request.Id, reviewedPickupSectionId: request.PickupSectionId);
		if (request.State != LibraryReservationState.NeedsReview) preview.BlockedReasons.Add("This request does not require review.");
		return new() { ReservationId = request.Id, CanonicalMemberId = preview.CanonicalMemberId, AcademicYearId = preview.AcademicYearId,
			Policy = preview.Policy, RequiresExplicitExpiry = preview.RequiresExplicitExpiry,
			CanReview = preview.BlockedReasons.Count == 0, BlockedReasons = preview.BlockedReasons };
	}

	public async Task<LibraryReservationDto> CreateAsync(LibraryReservationCreateRequest input)
	{
		await EnsurePermissionAsync(write: true);
		await access.GetContextAsync(input.AcademicYearId);
		await access.EnsureMemberAsync(input.MemberId);
		var request = await coordinator.CreateAsync(input.BookId, input.MemberId, input.AcademicYearId, input.RequestedDate,
			input.RequestExpiryDate, Actor(), pickupSectionId: input.PickupSectionId);
		await notifications.SendReservationConfirmedAsync(request, await books.GetAsync(request.BookId));
		return await MapAsync(request);
	}

	public async Task<PaginatedResponse<LibraryReservationDto>> GetListAsync(Guid bookId, Guid academicYearId,
		int page = 1, int pageSize = 20, bool includeHistory = false)
	{
		await EnsurePermissionAsync();
		await access.GetContextAsync(academicYearId);
		await books.GetAsync(bookId);
		return await ListAsync(bookId, page, pageSize, includeHistory, academicYearId);
	}

	public async Task<LibraryReservationDto> GetAsync(Guid reservationId, Guid academicYearId) =>
		await MapAsync(await ReadAsync(reservationId, academicYearId));

	public async Task<LibraryReservationDto> CancelAsync(Guid reservationId, LibraryReservationActionRequest input)
	{
		await EnsurePermissionAsync(write: true);
		var request = await ReadAsync(reservationId, input.AcademicYearId);
		await coordinator.CancelAsync(request, input.Reason, input.Revision, Actor());
		return await MapAsync(request);
	}

	public async Task<LibraryReservationDto> ReviewAsync(Guid reservationId, LibraryReservationReviewRequest input)
	{
		await EnsurePermissionAsync(write: true);
		var request = await ReadAsync(reservationId);
		if (!(await access.GetContextAsync(input.AcademicYearId)).UserScope.HasTenantWideScope())
			throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		await access.EnsureMemberAsync(input.MemberId);
		await coordinator.ReviewAsync(request, input.MemberId, input.AcademicYearId, input.RequestedDate,
			input.RequestExpiryDate, input.Reason, input.Revision, Actor());
		return await MapAsync(request);
	}

	public async Task<PaginatedResponse<LibraryReservationEventDto>> GetEventsAsync(Guid reservationId, Guid academicYearId, int page = 1, int pageSize = 20)
	{
		await ReadAsync(reservationId, academicYearId);
		page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
		var query = (await events.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id && row.ReservationId == reservationId);
		var count = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query.OrderByDescending(row => row.Sequence).Skip((page - 1) * pageSize).Take(pageSize));
		return new() { TotalCount = count, CurrentPage = page, PageSize = pageSize, TotalPages = (int)Math.Ceiling(count / (double)pageSize),
			Items = rows.Select(row => new LibraryReservationEventDto { Id = row.Id, Sequence = row.Sequence, EventType = row.EventType,
				HoldId = row.HoldId, CopyId = row.CopyId, LoanId = row.LoanId, PreviousState = (int)row.PreviousState, NextState = (int)row.NextState,
				BeforeBorrowerScopeCaptureId = row.BeforeBorrowerScopeCaptureId, AfterBorrowerScopeCaptureId = row.AfterBorrowerScopeCaptureId,
				ActorKind = (int)row.ActorKind, ActorId = row.ActorId, SystemPurpose = (int?)row.SystemPurpose, Reason = row.Reason, OccurredAt = row.OccurredAt }).ToList() };
	}

	private async Task<BookReservation> ReadAsync(Guid id, Guid? yearId = null)
	{
		await EnsurePermissionAsync();
		var context = await access.GetContextAsync(yearId);
		var request = await requests.GetAsync(id);
		await books.GetAsync(request.BookId);
		var wide = context.UserScope.HasTenantWideScope();
		if (request.AcademicYearId.HasValue)
		{
			if (!context.AllowedAcademicYearIds.Contains(request.AcademicYearId.Value))
			{
				if (!wide || request.State != LibraryReservationState.NeedsReview) throw new BusinessException(UserScopeErrorCodes.AccessDenied);
			}
			else if (yearId.HasValue && yearId != request.AcademicYearId) throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		}
		else if (!wide) throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		if (!await storedScope.CanReadAsync(request.Id, context.UserScope)) throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		return request;
	}

	private async Task<PaginatedResponse<LibraryReservationDto>> ListAsync(Guid bookId, int page, int pageSize, bool history, Guid? yearId = null)
	{
		page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
		var context = await access.GetContextAsync(yearId);
		var wide = context.UserScope.HasTenantWideScope();
		var allowedYears = context.AllowedAcademicYearIds.ToArray();
		var query = (await storedScope.GetReadableRequestsAsync(context.UserScope)).Where(row => row.BookId == bookId);
		query = query.Where(row => row.AcademicYearId.HasValue &&
			(yearId.HasValue ? row.AcademicYearId == yearId.Value : Enumerable.Contains(allowedYears, row.AcademicYearId.Value)) ||
			wide && (!row.AcademicYearId.HasValue || row.State == LibraryReservationState.NeedsReview && !Enumerable.Contains(allowedYears, row.AcademicYearId.Value)));
		if (!history) query = query.Where(row => row.State == LibraryReservationState.Waiting || row.State == LibraryReservationState.Ready || row.State == LibraryReservationState.NeedsReview);
		var count = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query.OrderBy(row => row.QueueSequence).ThenBy(row => row.Id).Skip((page - 1) * pageSize).Take(pageSize));
		var result = new PaginatedResponse<LibraryReservationDto> { TotalCount = count, CurrentPage = page, PageSize = pageSize, TotalPages = (int)Math.Ceiling(count / (double)pageSize) };
		foreach (var row in rows) result.Items.Add(await MapAsync(row));
		return result;
	}

	private async Task<LibraryReservationDto> MapAsync(BookReservation request)
	{
		var latestHoldId = await AsyncExecuter.FirstOrDefaultAsync((await events.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id &&
			row.ReservationId == request.Id && row.HoldId.HasValue).OrderByDescending(row => row.Sequence).Select(row => row.HoldId));
		LibraryHold? hold;
		if (latestHoldId.HasValue)
			hold = await holds.FindAsync(row => row.Id == latestHoldId.Value && row.ReservationId == request.Id && row.TenantId == CurrentTenant.Id);
		else
		{
			var unjournaled = await holds.GetListAsync(row => row.TenantId == CurrentTenant.Id && row.ReservationId == request.Id);
			hold = unjournaled.SingleOrDefault(row => row.State == LibraryHoldState.Allocated);
			if (hold == null && unjournaled.Count == 1) hold = unjournaled[0];
		}
		var update = await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Reserve);
		var result = new LibraryReservationDto { Id = request.Id, BookId = request.BookId, MemberId = request.CanonicalMemberId ?? request.MemberId,
			AcademicYearId = request.AcademicYearId, State = (int)request.State, LegacyRequestedDate = request.RequestedDate,
			LegacyReserveDueDate = request.ReserveDueDate, RequestedDate = request.SchoolRequestedDate, RequestExpiryDate = request.SchoolRequestExpiryDate,
			PickupCampusId = request.PickupCampusId, PickupSectionId = request.PickupSectionId, PickupLabel = request.PickupLabel,
			RequestedDateProvenance = (int)request.RequestedDateProvenance, RequestExpiryDateProvenance = (int)request.RequestExpiryDateProvenance,
			RequiresReview = request.State == LibraryReservationState.NeedsReview, ReviewReason = request.ReviewReason, Revision = request.Revision,
			BorrowerScopeCaptureId = request.BorrowerScopeCaptureId, BorrowerScopeCaptureVersion = request.BorrowerScopeCaptureVersion,
			BorrowerScopeProvenance = (int)request.BorrowerScopeProvenance,
			CanCancel = update && request.State is LibraryReservationState.Waiting or LibraryReservationState.Ready or LibraryReservationState.NeedsReview,
			CanReview = update && request.State == LibraryReservationState.NeedsReview && (await access.GetContextAsync()).UserScope.HasTenantWideScope(),
			Policy = request.PolicySnapshotJson == null ? null : JsonSerializer.Deserialize<LibraryBorrowingPolicySnapshotDto>(request.PolicySnapshotJson) };
		var label = (await memberReader.ReadLabelsAsync([result.MemberId])).GetValueOrDefault(result.MemberId);
		result.MemberName = label?.MemberName;
		result.MemberIdentifier = label?.MemberIdentifier;
		result.ClassDivision = label?.ClassDivision;
		result.Role = request.MemberRole ?? label?.Role;
		if (hold != null) result.Hold = new() { Id = hold.Id, CopyId = hold.CopyId, State = (int)hold.State, AllocatedAt = hold.AllocatedAt,
			PickupDeadline = hold.SchoolPickupDeadline, PickupDeadlineProvenance = (int)hold.PickupDeadlineProvenance,
			TimeZoneId = hold.TimeZoneId, RelatedLoanId = hold.RelatedLoanId,
			AccessionNumber = (await copies.FindAsync(hold.CopyId))?.AccessionNumber ?? string.Empty };
		return result;
	}

	private LibraryOperationActor Actor() => LibraryOperationActor.User(CurrentUser.Id ?? throw new AbpAuthorizationException());
	private async Task EnsurePermissionAsync(bool write = false)
	{
		if (!await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.AllBooks.View) ||
			write && !await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Reserve)) throw new AbpAuthorizationException();
		if (!CurrentTenant.Id.HasValue) throw new BusinessException(UserScopeErrorCodes.AccessDenied);
	}
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Shared;
using Volo.Abp;

namespace ShikshaMitra.Library.Reservations;

public partial class LibraryReservationAppService
{
	public async Task<GenericCreatedResponse> CreateLegacyAsync(Guid bookId, CreateBookReservationRequest input)
	{
		await EnsurePermissionAsync(write: true);
		await access.EnsureMemberAsync(input.MemberId);
		var facts = (await eligibility.ReadAsync([input.MemberId])).GetValueOrDefault(input.MemberId)
			?? throw new BusinessException(LibraryErrorCodes.MemberNotFound);
		var year = input.AcademicYearId ?? facts.AcademicYearId
			?? throw new BusinessException(LibraryErrorCodes.AcademicYearRequired, "Select an academic year for this staff reservation.");
		await access.GetContextAsync(year);
		var request = await coordinator.CreateAsync(bookId, input.MemberId, year, DateOnly.FromDateTime(input.RequestedDate),
			DateOnly.FromDateTime(input.ReserveDueDate), Actor(), input.RequestedDate, input.ReserveDueDate);
		await notifications.SendReservationConfirmedAsync(request, await books.GetAsync(bookId));
		return new() { Id = request.Id.ToString(), Message = "Book reservation created successfully." };
	}

	public async Task CancelLegacyAsync(Guid bookId, Guid reservationId)
	{
		await EnsurePermissionAsync(write: true);
		await books.GetAsync(bookId);
		var request = await ReadAsync(reservationId);
		if (request.BookId != bookId) throw new BusinessException(LibraryErrorCodes.ReservationNotFound);
		await coordinator.CancelAsync(request, "Cancelled through the reservation endpoint.", request.Revision, Actor());
	}

	public async Task<PaginatedResponse<BookReservationDto>> GetLegacyListAsync(Guid bookId, int page, int pageSize)
	{
		await EnsurePermissionAsync();
		await access.GetContextAsync();
		await books.GetAsync(bookId);
		var current = await ListAsync(bookId, page, pageSize, history: false);
		var result = new PaginatedResponse<BookReservationDto> { TotalCount = current.TotalCount, CurrentPage = current.CurrentPage,
			PageSize = current.PageSize, TotalPages = current.TotalPages };
		foreach (var item in current.Items) result.Items.Add(new() { Id = item.Id, BookId = item.BookId, MemberId = item.MemberId,
			RequestedDate = item.LegacyRequestedDate, ReserveDueDate = item.LegacyReserveDueDate, SchoolRequestedDate = item.RequestedDate,
			SchoolRequestExpiryDate = item.RequestExpiryDate, State = item.State, Revision = item.Revision, RequiresReview = item.RequiresReview, Hold = item.Hold });
		return result;
	}
}

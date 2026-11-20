using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.Reservations;

public interface ILibraryReservationAppService : IApplicationService
{
	Task<List<LibraryReservationPickupDto>> GetPickupLocationsAsync(Guid academicYearId);
	Task<LibraryReservationDto> CreateAsync(LibraryReservationCreateRequest input);
	Task<LibraryReservationPreviewDto> PreviewAsync(LibraryReservationCreateRequest input);
	Task<LibraryReservationReviewPreviewDto> PreviewReviewAsync(Guid reservationId, LibraryReservationReviewPreviewRequest input);
	Task<PaginatedResponse<LibraryReservationDto>> GetListAsync(Guid bookId, Guid academicYearId, int page = 1, int pageSize = 20, bool includeHistory = false);
	Task<LibraryReservationDto> GetAsync(Guid reservationId, Guid academicYearId);
	Task<LibraryReservationDto> CancelAsync(Guid reservationId, LibraryReservationActionRequest input);
	Task<LibraryReservationDto> ReviewAsync(Guid reservationId, LibraryReservationReviewRequest input);
	Task<PaginatedResponse<LibraryReservationEventDto>> GetEventsAsync(Guid reservationId, Guid academicYearId, int page = 1, int pageSize = 20);
}

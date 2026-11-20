using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.Portal;

public interface ILibraryPortalAppService : IApplicationService
{
	Task<List<LibraryPortalStudent>> GetStudentsAsync();
	Task<PaginatedResponse<LibraryPortalBook>> SearchAsync(Guid studentId, string? search, int page = 1, int pageSize = 20);
	Task<PaginatedResponse<LibraryPortalLoan>> GetLoansAsync(Guid studentId, int page = 1, int pageSize = 20);
	Task<PaginatedResponse<LibraryPortalReservation>> GetReservationsAsync(Guid studentId, int page = 1, int pageSize = 20);
	Task<List<LibraryReservationPickupDto>> GetPickupLocationsAsync(Guid studentId);
	Task<LibraryPortalReservePreview> PreviewReserveAsync(Guid studentId, LibraryPortalReserveInput input);
	Task<LibraryPortalReservation> ReserveAsync(Guid studentId, LibraryPortalReserveInput input);
	Task<LibraryPortalReservation> CancelAsync(Guid studentId, Guid id, LibraryPortalCancelInput input);
	Task<LibraryPortalRenewPreview> PreviewRenewAsync(Guid studentId, Guid loanId);
	Task<LibraryPortalLoan> RenewAsync(Guid studentId, Guid loanId, LibraryPortalRenewInput input);
	Task<List<LibraryPortalReceiptYear>> GetReceiptYearsAsync(Guid studentId);
	Task<PaginatedResponse<LibraryPortalReceiptDto>> GetReceiptsAsync(Guid studentId, Guid academicYearId,
		int page = 1, int pageSize = 20);
}

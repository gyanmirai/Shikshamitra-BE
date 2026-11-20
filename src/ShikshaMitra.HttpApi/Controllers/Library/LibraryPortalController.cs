using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Finance.Mobile;
using ShikshaMitra.Library.Portal;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Shared;
using Volo.Abp.AspNetCore.Mvc;
using Actions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.Portal;

namespace ShikshaMitra.HttpApi.Controllers.Library;

[ApiController]
[Authorize(Actions.View)]
[Route("api/mobile/v1")]
public class LibraryPortalController(ILibraryPortalAppService portal) : AbpController
{
	[HttpGet("library/students")]
	public Task<List<LibraryPortalStudent>> GetStudentsAsync() => portal.GetStudentsAsync();

	[Authorize(MobileFinancePermissions.ViewReceipts)]
	[HttpGet("students/{studentId:guid}/library/receipt-years")]
	public Task<List<LibraryPortalReceiptYear>> GetReceiptYearsAsync(Guid studentId) => portal.GetReceiptYearsAsync(studentId);

	[Authorize(MobileFinancePermissions.ViewReceipts)]
	[HttpGet("students/{studentId:guid}/library/receipts")]
	public Task<PaginatedResponse<LibraryPortalReceiptDto>> GetReceiptsAsync(Guid studentId, [FromQuery] Guid academicYearId,
		[FromQuery] int page = 1, [FromQuery] int pageSize = 20) => portal.GetReceiptsAsync(studentId, academicYearId, page, pageSize);

	[HttpGet("students/{studentId:guid}/library/catalogue")]
	public Task<PaginatedResponse<LibraryPortalBook>> SearchAsync(Guid studentId, [FromQuery] string? search,
		[FromQuery] int page = 1, [FromQuery] int pageSize = 20) => portal.SearchAsync(studentId, search, page, pageSize);

	[HttpGet("students/{studentId:guid}/library/loans")]
	public Task<PaginatedResponse<LibraryPortalLoan>> GetLoansAsync(Guid studentId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
		portal.GetLoansAsync(studentId, page, pageSize);

	[HttpGet("students/{studentId:guid}/library/reservations")]
	public Task<PaginatedResponse<LibraryPortalReservation>> GetReservationsAsync(Guid studentId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
		portal.GetReservationsAsync(studentId, page, pageSize);

	[Authorize(Actions.Reserve)]
	[HttpGet("students/{studentId:guid}/library/pickup-locations")]
	public Task<List<LibraryReservationPickupDto>> GetPickupLocationsAsync(Guid studentId) => portal.GetPickupLocationsAsync(studentId);

	[Authorize(Actions.Reserve)]
	[HttpPost("students/{studentId:guid}/library/reservations/preview")]
	public Task<LibraryPortalReservePreview> PreviewReserveAsync(Guid studentId, [FromBody] LibraryPortalReserveInput input) =>
		portal.PreviewReserveAsync(studentId, input);

	[Authorize(Actions.Reserve)]
	[HttpPost("students/{studentId:guid}/library/reservations")]
	public Task<LibraryPortalReservation> ReserveAsync(Guid studentId, [FromBody] LibraryPortalReserveInput input) => portal.ReserveAsync(studentId, input);

	[Authorize(Actions.Reserve)]
	[HttpPost("students/{studentId:guid}/library/reservations/{id:guid}/cancel")]
	public Task<LibraryPortalReservation> CancelAsync(Guid studentId, Guid id, [FromBody] LibraryPortalCancelInput input) =>
		portal.CancelAsync(studentId, id, input);

	[Authorize(Actions.Renew)]
	[HttpGet("students/{studentId:guid}/library/loans/{loanId:guid}/renewal-preview")]
	public Task<LibraryPortalRenewPreview> PreviewRenewAsync(Guid studentId, Guid loanId) => portal.PreviewRenewAsync(studentId, loanId);

	[Authorize(Actions.Renew)]
	[HttpPost("students/{studentId:guid}/library/loans/{loanId:guid}/renew")]
	public Task<LibraryPortalLoan> RenewAsync(Guid studentId, Guid loanId, [FromBody] LibraryPortalRenewInput input) =>
		portal.RenewAsync(studentId, loanId, input);
}

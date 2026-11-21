using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Shared;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Library;

[Area("Library")]
[Route("api/library/reservations")]
public class LibraryReservationController(ILibraryReservationAppService reservations) : AbpController
{
	[HttpGet("pickup-locations")]
	public Task<List<LibraryReservationPickupDto>> GetPickupLocationsAsync([FromQuery] Guid academicYearId) =>
		reservations.GetPickupLocationsAsync(academicYearId);

	[HttpPost("preview")]
	public Task<LibraryReservationPreviewDto> PreviewAsync([FromBody] LibraryReservationCreateRequest input) => reservations.PreviewAsync(input);
	[HttpPost("{reservationId:guid}/review-preview")]
	public Task<LibraryReservationReviewPreviewDto> PreviewReviewAsync(Guid reservationId, [FromBody] LibraryReservationReviewPreviewRequest input) => reservations.PreviewReviewAsync(reservationId, input);
	[HttpPost]
	public Task<LibraryReservationDto> CreateAsync([FromBody] LibraryReservationCreateRequest input) => reservations.CreateAsync(input);
	[HttpGet]
	public Task<PaginatedResponse<LibraryReservationDto>> GetListAsync([FromQuery] Guid bookId, [FromQuery] Guid academicYearId,
		[FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] bool includeHistory = false) =>
		reservations.GetListAsync(bookId, academicYearId, page, pageSize, includeHistory);
	[HttpGet("{reservationId:guid}")]
	public Task<LibraryReservationDto> GetAsync(Guid reservationId, [FromQuery] Guid academicYearId) => reservations.GetAsync(reservationId, academicYearId);
	[HttpPost("{reservationId:guid}/cancel")]
	public Task<LibraryReservationDto> CancelAsync(Guid reservationId, [FromBody] LibraryReservationActionRequest input) => reservations.CancelAsync(reservationId, input);
	[HttpPost("{reservationId:guid}/review")]
	public Task<LibraryReservationDto> ReviewAsync(Guid reservationId, [FromBody] LibraryReservationReviewRequest input) => reservations.ReviewAsync(reservationId, input);
	[HttpGet("{reservationId:guid}/events")]
	public Task<PaginatedResponse<LibraryReservationEventDto>> GetEventsAsync(Guid reservationId, [FromQuery] Guid academicYearId,
		[FromQuery] int page = 1, [FromQuery] int pageSize = 20) => reservations.GetEventsAsync(reservationId, academicYearId, page, pageSize);
}

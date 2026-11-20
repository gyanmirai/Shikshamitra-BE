using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Shared;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Library;

[Area("Library")]
[Route("api/library/reservations/members")]
public class LibraryReservationMemberController(ILibraryReservationMemberAppService members) : AbpController
{
	[HttpGet]
	public Task<PaginatedResponse<LibraryReservationMemberDto>> SearchAsync([FromQuery] Guid academicYearId, [FromQuery] string? search = null,
		[FromQuery] int page = 1, [FromQuery] int pageSize = 20) => members.SearchAsync(academicYearId, search, page, pageSize);
	[HttpGet("resolve")]
	public Task<LibraryReservationMemberDto> ResolveAsync([FromQuery] Guid academicYearId, [FromQuery] string identifier) => members.ResolveAsync(academicYearId, identifier);
}

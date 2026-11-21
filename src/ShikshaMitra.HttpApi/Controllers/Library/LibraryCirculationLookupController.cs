using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Shared;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Library;

[Authorize]
[Area("Library")]
[Route("api/library/circulation/lookups")]
public class LibraryCirculationLookupController(ILibraryCirculationLookupAppService lookups) : AbpController
{
	[HttpGet("assessment-categories")]
	public Task<PaginatedResponse<LibraryReturnAssessmentCategoryDto>> GetAssessmentCategoriesAsync([FromQuery] Guid academicYearId,
		[FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null) =>
		lookups.GetAssessmentCategoriesAsync(academicYearId, page, pageSize, search);
	[HttpGet("copy")]
	public Task<LibraryCopyLookupDto> GetCopyAsync([FromQuery] string scan) => lookups.GetCopyAsync(scan);
	[HttpGet("return")]
	public Task<LibraryReturnLookupDto> GetReturnAsync([FromQuery] string scan) => lookups.GetReturnAsync(scan);
}

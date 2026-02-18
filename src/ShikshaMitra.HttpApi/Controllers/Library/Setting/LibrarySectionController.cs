using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Library.Setting.Sections;
using ShikshaMitra.Library.Setting.Sections.Dtos;
using ShikshaMitra.Library.Shared;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Library.Setting;

[Area("Library")]
[Route("api/sections")]
public class LibrarySectionController : AbpController
{
	private readonly ILibrarySectionAppService _service;

	public LibrarySectionController(ILibrarySectionAppService service)
	{
		_service = service;
	}

	[HttpGet]
	public Task<PaginatedSectionResponse> GetListAsync(
		[FromQuery] Guid academicYearId,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 10)
	{
		return _service.GetListAsync(academicYearId, page, pageSize);
	}

	[HttpPost]
	public async Task<IActionResult> CreateAsync(
		[FromQuery] Guid academicYearId,
		[FromBody] SectionCreateRequest input)
	{
		var result = await _service.CreateAsync(academicYearId, input);
		return StatusCode(201, result);
	}

	[HttpGet("{sectionId}")]
	public Task<SectionDetailsResponse> GetAsync(string sectionId, [FromQuery] Guid academicYearId)
	{
		if (!Guid.TryParse(sectionId, out var id))
			throw new ArgumentException("Invalid sectionId format.");
		return _service.GetAsync(id, academicYearId);
	}

	[HttpPut("{sectionId}")]
	public Task<GenericUpdatedResponse> UpdateAsync(string sectionId, [FromQuery] Guid academicYearId, [FromBody] SectionUpdateRequest input)
	{
		if (!Guid.TryParse(sectionId, out var id))
			throw new ArgumentException("Invalid sectionId format.");
		return _service.UpdateAsync(id, academicYearId, input);
	}

	// Contract specifies DELETE at library/sections/{sectionId}
	[HttpDelete("/library/sections/{sectionId}")]
	public async Task<IActionResult> DeleteAsync(string sectionId, [FromQuery] Guid academicYearId)
	{
		if (!Guid.TryParse(sectionId, out var id))
			throw new ArgumentException("Invalid sectionId format.");
		await _service.DeleteAsync(id, academicYearId);
		return Ok(new { });
	}
}

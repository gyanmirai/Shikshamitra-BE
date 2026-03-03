using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Library.Setting.ChargeCategory;
using ShikshaMitra.Library.Setting.ChargeCategory.Dtos;
using ShikshaMitra.Library.Shared;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Library.Setting;

[Area("Library")]
[Route("api/library/charge-categories")]
public class LibraryChargeCategoryController : AbpController
{
	private readonly ILibraryChargeCategoryAppService _service;

	public LibraryChargeCategoryController(ILibraryChargeCategoryAppService service)
	{
		_service = service;
	}

	[HttpGet]
	public Task<PaginatedLibraryChargeCategoryResponse> GetListAsync(
		[FromQuery] Guid academicYearId,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 10)
	{
		return _service.GetListAsync(academicYearId, page, pageSize);
	}

	[HttpPost]
	public async Task<IActionResult> CreateAsync(
		[FromQuery] Guid academicYearId,
		[FromBody] LibraryChargeCategoryCreateRequest input)
	{
		var result = await _service.CreateAsync(academicYearId, input);
		return StatusCode(201, result);
	}

	[HttpGet("{categoryId:guid}")]
	public Task<LibraryChargeCategoryDto> GetAsync(Guid categoryId)
	{
		return _service.GetAsync(categoryId);
	}

	[HttpPut("{categoryId:guid}")]
	public Task<GenericUpdatedResponse> UpdateAsync(Guid categoryId, [FromBody] LibraryChargeCategoryUpdateRequest input)
	{
		return _service.UpdateAsync(categoryId, input);
	}

	[HttpDelete("{categoryId:guid}")]
	public async Task<IActionResult> DeleteAsync(Guid categoryId)
	{
		await _service.DeleteAsync(categoryId);
		return Ok(new { });
	}
}

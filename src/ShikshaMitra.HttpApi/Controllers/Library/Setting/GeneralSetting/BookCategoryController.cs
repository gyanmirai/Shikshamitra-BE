using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Shared;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Library.Setting.GeneralSetting;

[Area("Library")]
[Route("api/book-categories")]
public class BookCategoryController : AbpController
{
	private readonly IBookCategoryAppService _bookCategoryAppService;

	public BookCategoryController(IBookCategoryAppService bookCategoryAppService)
	{
		_bookCategoryAppService = bookCategoryAppService;
	}

	[HttpGet]
	public Task<PaginatedResponse<BookCategoryDto>> GetListAsync(
		[FromQuery] Guid academicYearId,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 10)
	{
		return _bookCategoryAppService.GetListAsync(academicYearId, page, pageSize);
	}

	[HttpPost]
	public async Task<IActionResult> CreateAsync(
		[FromQuery] string? academicYearId,
		[FromBody] BookCategoryCreateRequest input)
	{
		Guid? parsedId = Guid.TryParse(academicYearId, out var g) ? g : null;
		var result = await _bookCategoryAppService.CreateAsync(parsedId, input);
		return StatusCode(201, result);
	}

	[HttpGet("{categoryId:guid}")]
	public Task<BookCategoryDto> GetAsync(Guid categoryId)
	{
		return _bookCategoryAppService.GetAsync(categoryId);
	}

	[HttpPut("{categoryId:guid}")]
	public Task<GenericUpdatedResponse> UpdateAsync(Guid categoryId, [FromBody] BookCategoryUpdateRequest input)
	{
		return _bookCategoryAppService.UpdateAsync(categoryId, input);
	}

	[HttpDelete("{categoryId:guid}")]
	public async Task<IActionResult> DeleteAsync(Guid categoryId)
	{
		await _bookCategoryAppService.DeleteAsync(categoryId);
		return Ok(new { });
	}
}

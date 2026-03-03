using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Shared;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Library;

[Area("Library")]
[Route("api/library/books")]
public class BookController : AbpController
{
    private readonly IBookAppService _bookAppService;

    public BookController(IBookAppService bookAppService)
    {
        _bookAppService = bookAppService;
    }

    [HttpGet]
    public Task<PaginatedResponse<BookDto>> GetListAsync(
        [FromQuery] Guid academicYearId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        return _bookAppService.GetListAsync(academicYearId, page, pageSize);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync(
        [FromQuery] string? academicYearId,
        [FromBody] CreateBookRequest input)
    {
        Guid? parsedId = Guid.TryParse(academicYearId, out var g) ? g : null;
        var result = await _bookAppService.CreateAsync(parsedId, input);
        return StatusCode(201, result);
    }

    [HttpGet("{bookId:guid}")]
    public Task<BookDto> GetAsync(Guid bookId)
    {
        return _bookAppService.GetAsync(bookId);
    }

    [HttpPut("{bookId:guid}")]
    public Task<GenericUpdatedResponse> UpdateAsync(Guid bookId, [FromBody] UpdateBookRequest input)
    {
        return _bookAppService.UpdateAsync(bookId, input);
    }

    [HttpDelete("{bookId:guid}")]
    public async Task<IActionResult> DeleteAsync(Guid bookId)
    {
        await _bookAppService.DeleteAsync(bookId);
        return Ok(new { });
    }

    [HttpPut("{bookId:guid}/copies")]
    public async Task<IActionResult> PutCopiesAsync(Guid bookId, [FromBody] PutBookCopiesRequest input)
    {
        var result = await _bookAppService.PutCopiesAsync(bookId, input);
        return Ok(result);
    }
}

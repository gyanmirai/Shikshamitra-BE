using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Shared;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Library;

[Area("Library")]
[Route("api/library")]
public class BookIssueController : AbpController
{
    private readonly IBookIssueAppService _bookIssueAppService;

    public BookIssueController(IBookIssueAppService bookIssueAppService)
    {
        _bookIssueAppService = bookIssueAppService;
    }

    [HttpPost("issues")]
    public async Task<IActionResult> IssueAsync([FromBody] IssueBookRequest input)
    {
        var result = await _bookIssueAppService.IssueAsync(input);
        return StatusCode(201, result);
    }

    [HttpPost("returns")]
    public async Task<IActionResult> ReturnAsync([FromBody] ReturnBookRequest input)
    {
        var result = await _bookIssueAppService.ReturnAsync(input);
        return StatusCode(201, result);
    }

    [HttpGet("books/accession/{accessionNo}")]
    public Task<AccessionBookDto> GetByAccessionAsync(string accessionNo)
    {
        return _bookIssueAppService.GetByAccessionAsync(accessionNo);
    }

    [HttpGet("members/{memberId:guid}")]
    public Task<LibraryMemberDto> GetMemberAsync(Guid memberId)
    {
        return _bookIssueAppService.GetMemberAsync(memberId);
    }

    [HttpGet("issue-returns")]
    public Task<PaginatedResponse<IssueReturnItemDto>> GetIssueReturnListAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        return _bookIssueAppService.GetIssueReturnListAsync(page, pageSize);
    }
}

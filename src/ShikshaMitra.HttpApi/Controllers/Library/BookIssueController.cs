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
    [HttpPost("return")]
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

    [HttpGet("issue-returns")]
    [HttpGet("issue-return")]
    public Task<PaginatedResponse<IssueReturnItemDto>> GetIssueReturnListAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        return _bookIssueAppService.GetIssueReturnListAsync(page, pageSize);
    }

    [HttpGet("return-book/search")]
    [HttpGet("returns/search")]
    public Task<ReturnBookSearchDto> SearchReturnBookAsync([FromQuery] string accessionNo)
    {
        return _bookIssueAppService.SearchReturnBookAsync(accessionNo);
    }

    [HttpGet("return/{accessionNo}")]
    public Task<ReturnBookSearchDto> SearchReturnBookByRouteAsync(string accessionNo)
    {
        return _bookIssueAppService.SearchReturnBookAsync(accessionNo);
    }

    [HttpGet("issuedBook/accession/{accessionNo}/member")]
    public async Task<LibraryMemberDto> GetIssuedBookMemberByAccessionAsync(string accessionNo)
    {
        var result = await _bookIssueAppService.SearchReturnBookAsync(accessionNo);
        return await _bookIssueAppService.GetMemberAsync(result.MemberDetails.Id);
    }
}

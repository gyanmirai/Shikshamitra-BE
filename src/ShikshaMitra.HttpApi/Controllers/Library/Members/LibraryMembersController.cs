using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Members;
using ShikshaMitra.Library.Shared;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Library.Members;

[RemoteService(Name = "LibraryMember")]
[Area("Library")]
[Route("api/library/members")]
public class LibraryMembersController(
    ILibraryMemberAppService libraryMemberAppService,
    IBookIssueAppService bookIssueAppService) : AbpController
{
    [HttpGet]
    public async Task<PaginatedResponse<LibraryMemberDto>> GetListAsync([FromQuery] GetLibraryMembersInput input)
    {
        return await libraryMemberAppService.GetListAsync(input);
    }

    [HttpGet("{memberId:guid}")]
    public Task<LibraryMemberDto> GetAsync(Guid memberId)
    {
        return bookIssueAppService.GetMemberAsync(memberId);
    }

    [HttpGet("{memberId:guid}/borrowed-books")]
    [HttpGet("{memberId:guid}/borrowed")]
    public Task<PaginatedResponse<MemberBorrowedBookDto>> GetBorrowedBooksAsync(
        Guid memberId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        return libraryMemberAppService.GetBorrowedBooksAsync(memberId, page, pageSize);
    }

    [HttpGet("{memberId:guid}/reserved-books")]
    [HttpGet("{memberId:guid}/reserved")]
    public Task<PaginatedResponse<MemberReservedBookDto>> GetReservedBooksAsync(
        Guid memberId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        return libraryMemberAppService.GetReservedBooksAsync(memberId, page, pageSize);
    }

    [HttpGet("{memberId:guid}/charges")]
    public Task<PaginatedResponse<MemberChargeItemDto>> GetChargesAsync(
        Guid memberId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        return libraryMemberAppService.GetMemberChargesAsync(memberId, page, pageSize);
    }

    [HttpGet("{memberId:guid}/borrow-history")]
    public Task<PaginatedResponse<MemberBorrowHistoryDto>> GetBorrowHistoryAsync(
        Guid memberId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        return libraryMemberAppService.GetBorrowHistoryAsync(memberId, page, pageSize);
    }
}

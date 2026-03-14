using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Members;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Library.Members;

[RemoteService(Name = "LibraryMember")]
[Area("Library")]
[Route("api/library/members")]
public class LibraryMembersController(ILibraryMemberAppService libraryMemberAppService) : AbpController
{
    [HttpGet]
    public async Task<PagedResultDto<LibraryMemberDto>> GetListAsync([FromQuery] GetLibraryMembersInput input)
    {
        return await libraryMemberAppService.GetListAsync(input);
    }
}
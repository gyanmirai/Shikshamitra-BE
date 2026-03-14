using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.IssueReturn;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.Members;

public interface ILibraryMemberAppService : IApplicationService
{
	Task CreateLibraryMember(Guid studentId, bool isActive = true);

	Task HandleLibraryMemberAsync(Guid studentId, bool isActive = true);

	Task<PagedResultDto<LibraryMemberDto>> GetListAsync(GetLibraryMembersInput input);
}

using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.Members;

public interface ILibraryMemberAppService : IApplicationService
{
	Task CreateLibraryMember(Guid studentId, bool isActive = true);

	Task HandleLibraryMemberAsync(Guid studentId, bool isActive = true);
}

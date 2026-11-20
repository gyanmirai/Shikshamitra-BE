using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.Policies;

public interface ILibraryBorrowingPolicyAppService : IApplicationService
{
	Task<PaginatedResponse<LibraryBorrowingPolicyVersionDto>> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 20);
	Task<LibraryBorrowingPolicyVersionDto> GetAsync(Guid versionId);
	Task<LibraryBorrowingPolicyVersionDto> CreateAsync(Guid academicYearId, LibraryBorrowingPolicyDraftRequest input);
	Task<LibraryBorrowingPolicyVersionDto> UpdateAsync(Guid versionId, LibraryBorrowingPolicyDraftRequest input);
	Task<LibraryBorrowingPolicyVersionDto> PublishAsync(Guid versionId, LibraryPolicyPublishRequest input);
	Task<LibraryBorrowingPolicyVersionDto> ArchiveAsync(Guid versionId, LibraryPolicyArchiveRequest input);
	Task<LibraryBorrowingPolicySnapshotDto> ResolveAsync(LibraryPolicyResolveRequest input);
	Task<LibraryPolicyOptionsDto> GetOptionsAsync(Guid academicYearId);
}

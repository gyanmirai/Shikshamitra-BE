using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.Members;

public interface ILibraryMemberAppService : IApplicationService
{
	Task CreateLibraryMember(Guid studentId, bool isActive = true);

	Task HandleLibraryMemberAsync(Guid studentId, bool isActive = true);

	Task<PagedResultDto<LibraryMemberDto>> GetListAsync(GetLibraryMembersInput input);

	Task<PaginatedResponse<MemberBorrowedBookDto>> GetBorrowedBooksAsync(Guid memberId, int page = 1, int pageSize = 10);

	Task<PaginatedResponse<MemberReservedBookDto>> GetReservedBooksAsync(Guid memberId, int page = 1, int pageSize = 10);

	Task<PaginatedResponse<MemberChargeItemDto>> GetMemberChargesAsync(Guid memberId, int page = 1, int pageSize = 10);

	Task<PaginatedResponse<MemberBorrowHistoryDto>> GetBorrowHistoryAsync(Guid memberId, int page = 1, int pageSize = 10);
}

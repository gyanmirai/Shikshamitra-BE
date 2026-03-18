using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.IssueReturn;

public interface IBookIssueAppService : IApplicationService
{
    Task<GenericCreatedResponse> IssueAsync(IssueBookRequest input);
    Task<GenericCreatedResponse> ReturnAsync(ReturnBookRequest input);
    Task<AccessionBookDto> GetByAccessionAsync(string accessionNo);
    Task<LibraryMemberDto> GetMemberAsync(Guid memberId);
    Task<PaginatedResponse<IssueReturnItemDto>> GetIssueReturnListAsync(int page = 1, int pageSize = 20);
    Task<ReturnBookSearchDto> SearchReturnBookAsync(string accessionNo);
}

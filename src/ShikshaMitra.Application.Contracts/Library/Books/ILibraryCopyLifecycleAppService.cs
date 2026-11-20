using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.Books;

public interface ILibraryCopyLifecycleAppService : IApplicationService
{
	Task<LibraryCopyStateDto> GetStateAsync(Guid bookId, Guid copyId);
	Task<LibraryCopyMoveOptionsDto> GetMoveOptionsAsync(Guid bookId, Guid copyId, Guid? academicYearId = null);
	Task<PaginatedResponse<LibraryCopyEventDto>> GetEventsAsync(Guid bookId, Guid copyId, int page = 1, int pageSize = 20);
	Task<LibraryCopyStateDto> ChangeConditionAsync(Guid bookId, Guid copyId, LibraryCopyConditionRequest input);
	Task<LibraryCopyStateDto> MoveAsync(Guid bookId, Guid copyId, LibraryCopyMoveRequest input);
	Task<LibraryCopyStateDto> WriteOffAsync(Guid bookId, Guid copyId, LibraryCopyWriteOffRequest input);
}

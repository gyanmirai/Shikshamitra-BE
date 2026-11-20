using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.CampusTransfers;

public interface ILibraryCampusTransferAppService : IApplicationService
{
	Task<PaginatedResponse<LibraryCampusTransferListItem>> GetListAsync(int page = 1, int pageSize = 25);
	Task<LibraryCampusTransferDetail> GetAsync(Guid id);
	Task<List<LibraryCampusTransferSection>> GetLocationsAsync();
	Task<List<LibraryCampusTransferCopyOption>> FindCopiesAsync(Guid sourceSectionId, string search);
	Task<LibraryCampusTransferPreview> PreviewDispatchAsync(LibraryCampusDispatchInput input);
	Task<LibraryCampusTransferHeader> DispatchAsync(LibraryCampusDispatchCommand command);
	Task<LibraryCampusTransferPreview> PreviewReceiveAsync(Guid id, LibraryCampusReceiveInput input);
	Task<LibraryCampusTransferReceiptDto> ReceiveAsync(Guid id, LibraryCampusReceiveCommand command);
}

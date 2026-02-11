using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using ShikshaMitra.Application.Contracts.Finance.Setting.ReceiptBook.Dtos;
using ShikshaMitra.Application.Contracts.Finance.Setting.FeeCategory.Dtos; // For MessageResponse

namespace ShikshaMitra.Application.Contracts.Finance.Setting.ReceiptBook;

public interface IReceiptBookAppService : IApplicationService
{
    Task<ReceiptBookResponse> CreateAsync(Guid academicYearId, ReceiptBookCreateRequest input);
    Task<ReceiptBookListResponse> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10, string search = null);
    Task<ReceiptBookResponse> GetAsync(Guid receiptBookId);
    Task<MessageResponse> UpdateAsync(Guid receiptBookId, ReceiptBookUpdateRequest input);
    Task<MessageResponse> DeleteAsync(Guid receiptBookId);
    Task<ReceiptBookStatusUpdateResponse> UpdateStatusAsync(Guid receiptBookId, ReceiptBookStatusUpdateRequest input);
}

using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using ShikshaMitra.Finance.Fees.Settings.FeeCategory.Dtos;
using ShikshaMitra.Finance.Fees.Settings.Dtos;

namespace ShikshaMitra.Finance.Fees.Settings;

public interface IReceiptBookAppService : IApplicationService
{
    Task<ReceiptBookResponse> CreateAsync(Guid academicYearId, ReceiptBookCreateRequest input);
    Task<ReceiptBookListResponse> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10, string search = null);
    Task<ReceiptBookResponse> GetAsync(Guid receiptBookId);
    Task<MessageResponse> UpdateAsync(Guid receiptBookId, ReceiptBookUpdateRequest input);
    Task<MessageResponse> DeleteAsync(Guid receiptBookId);
    Task<ReceiptBookStatusUpdateResponse> UpdateStatusAsync(Guid receiptBookId, ReceiptBookStatusUpdateRequest input);
}

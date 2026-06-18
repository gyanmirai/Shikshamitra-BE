using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using ShikshaMitra.Finance.Fees.Settings.Dtos;
using ShikshaMitra.Finance.Fees.Settings;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Fees.Settings;

[RemoteService]
[Area("finance")]
[Route("api/receipt-books")]
[Authorize]
public class ReceiptBookController : AbpController
{
    private readonly IReceiptBookAppService _receiptBookAppService;

    public ReceiptBookController(IReceiptBookAppService receiptBookAppService)
    {
        _receiptBookAppService = receiptBookAppService;
    }

    /// <summary>
    /// Create a receipt book for a specific academic year
    /// </summary>
    /// <param name="academicYearId">Academic year identifier (UUID)</param>
    /// <param name="input">Receipt book creation details</param>
    /// <returns>Created receipt book details</returns>
    [HttpPost]
    public Task<ReceiptBookResponse> CreateAsync([FromQuery] Guid academicYearId, [FromBody] ReceiptBookCreateRequest input)
    {
        return _receiptBookAppService.CreateAsync(academicYearId, input);
    }

    /// <summary>
    /// Fetch receipt books by academic year (table view)
    /// </summary>
    /// <param name="academicYearId">Academic year identifier</param>
    /// <param name="page">Page number (1-indexed)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="search">Search by receipt book name</param>
    /// <returns>Paginated list of receipt books</returns>
    [HttpGet]
    public Task<ReceiptBookListResponse> GetListAsync(
        [FromQuery] Guid academicYearId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string search = null)
    {
        return _receiptBookAppService.GetListAsync(academicYearId, page, pageSize, search);
    }

    /// <summary>
    /// Get receipt book details
    /// </summary>
    /// <param name="receiptBookId">Receipt book identifier (UUID)</param>
    /// <returns>Receipt book details</returns>
    [HttpGet("{receiptBookId}")]
    public Task<ReceiptBookResponse> GetAsync(Guid receiptBookId)
    {
        return _receiptBookAppService.GetAsync(receiptBookId);
    }

    /// <summary>
    /// Update receipt book
    /// </summary>
    /// <param name="receiptBookId">Receipt book identifier (UUID)</param>
    /// <param name="input">Receipt book update details</param>
    /// <returns>Success message</returns>
    [HttpPut("{receiptBookId}")]
    public Task<MessageResponse> UpdateAsync(Guid receiptBookId, [FromBody] ReceiptBookUpdateRequest input)
    {
        return _receiptBookAppService.UpdateAsync(receiptBookId, input);
    }

    /// <summary>
    /// Soft delete receipt book
    /// </summary>
    /// <param name="receiptBookId">Receipt book identifier (UUID)</param>
    /// <returns>Success message</returns>
    [HttpDelete("{receiptBookId}")]
    public Task<MessageResponse> DeleteAsync(Guid receiptBookId)
    {
        return _receiptBookAppService.DeleteAsync(receiptBookId);
    }

    /// <summary>
    /// Activate or deactivate a receipt book
    /// </summary>
    /// <param name="receiptBookId">Receipt book identifier (UUID)</param>
    /// <param name="input">Status update request</param>
    /// <returns>Status update response</returns>
    [HttpPatch("{receiptBookId}/status")]
    public Task<ReceiptBookStatusUpdateResponse> UpdateStatusAsync(Guid receiptBookId, [FromBody] ReceiptBookStatusUpdateRequest input)
    {
        return _receiptBookAppService.UpdateStatusAsync(receiptBookId, input);
    }
}

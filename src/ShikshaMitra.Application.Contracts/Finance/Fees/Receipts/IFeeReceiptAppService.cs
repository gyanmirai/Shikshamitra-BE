using System;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Receipts.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Fees.Receipts;

/// <summary>
/// Application service for querying and downloading fee/charge receipts.
/// All mutating operations (collect, finalize, reject) live in separate services.
/// </summary>
public interface IFeeReceiptAppService : IApplicationService
{
    /// <summary>
    /// Returns a paginated, sorted list of receipts matching the supplied filter.
    /// Board + Medium are mandatory; Month and ReceiptKind are optional.
    /// Results are ordered by CreationTime DESC (newest first).
    /// </summary>
    Task<PagedResultDto<FeeReceiptListItemDto>> GetListAsync(
        GetFeeReceiptListInput input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the full detail of a single receipt, including sibling correlation.
    /// </summary>
    Task<FeeReceiptDetailDto> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the PDF bytes for the given receipt.
    /// If the PDF has not yet been generated it is generated lazily, stored in ABP Blob, and returned.
    /// </summary>
    Task<byte[]> GetPdfAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}

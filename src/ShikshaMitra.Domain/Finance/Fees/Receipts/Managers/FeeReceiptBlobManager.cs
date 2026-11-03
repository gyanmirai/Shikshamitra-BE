using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Services;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.Receipts;

/// <summary>
/// Domain service that owns all ABP Blob Storing interactions for fee/charge receipt PDFs.
/// <para>
/// Blob path pattern: <c>fee-receipts/{tenantId}/{receiptKind}/{receiptId}.pdf</c>
/// </para>
/// </summary>
public class FeeReceiptBlobManager(
    IBlobContainer blobContainer,
    ICurrentTenant currentTenant
) : DomainService
{
    /// <summary>
    /// Saves a PDF byte array to ABP Blob Storing and returns the blob name to be
    /// persisted on <see cref="FeePaymentReceipt.PdfBlobName"/>.
    /// </summary>
    public async Task<string> SavePdfAsync(
        Guid receiptId,
        FeeReceiptKind kind,
        byte[] pdfBytes,
        CancellationToken cancellationToken = default)
    {
        var blobName = BuildBlobName(receiptId, kind);
        using var stream = new MemoryStream(pdfBytes);
        await blobContainer.SaveAsync(blobName, stream, overrideExisting: true, cancellationToken: cancellationToken);
        return blobName;
    }

    /// <summary>
    /// Retrieves the PDF bytes for a receipt, or <c>null</c> if no blob has been stored yet.
    /// </summary>
    public async Task<byte[]?> GetPdfOrNullAsync(
        string blobName,
        CancellationToken cancellationToken = default)
    {
        var stream = await blobContainer.GetOrNullAsync(blobName, cancellationToken);
        if (stream == null) return null;

        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        return ms.ToArray();
    }

    /// <summary>
    /// Deletes a stored PDF blob. Safe to call when blob may not exist.
    /// </summary>
    public async Task DeletePdfIfExistsAsync(
        string blobName,
        CancellationToken cancellationToken = default)
    {
        await blobContainer.DeleteAsync(blobName, cancellationToken);
    }

    /// <summary>
    /// Constructs the canonical blob name for a receipt PDF.
    /// The tenant ID segment ensures isolation between tenants sharing the same
    /// default blob container.
    /// </summary>
    public string BuildBlobName(Guid receiptId, FeeReceiptKind kind)
    {
        var tenantSegment = currentTenant.Id?.ToString() ?? "host";
        return $"fee-receipts/{tenantSegment}/v4/{kind}/{receiptId}.pdf";
    }
}

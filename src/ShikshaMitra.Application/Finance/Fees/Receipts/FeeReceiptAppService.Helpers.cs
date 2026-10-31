using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Students;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Receipts.Dtos;
using Volo.Abp;

namespace ShikshaMitra.Finance.Fees.Receipts;

public partial class FeeReceiptAppService
{
    private static FeeReceiptPaymentInstrumentDto MapPaymentInstrument(PaymentMethodRecord payment)
    {
        var details = payment.PaymentDetails == null ? new Dictionary<string, string?>()
            : JsonSerializer.SerializeToElement<PaymentDetails>(payment.PaymentDetails,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)).EnumerateObject()
                .Where(p => p.Name != "$type")
                .ToDictionary(p => p.Name, p => p.Value.ValueKind == JsonValueKind.Null ? null : p.Value.ToString());
        return new FeeReceiptPaymentInstrumentDto
        {
            PaymentMethod = payment.PaymentMethod, Amount = payment.Amount, PaymentDate = payment.PaymentDate,
            BankAccountId = payment.BankAccountId, BankLabel = payment.BankLabelSnapshot, InstrumentDetails = details,
        };
    }

    public async Task<byte[]> GetPdfAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var receipt = await receiptRepository.GetAsync(id, cancellationToken: cancellationToken)
            ?? throw new UserFriendlyException(FeeReceiptErrorCodes.ReceiptNotFound, "Receipt not found.");
        await financeScopeAccessService.EnsureCollectionPayerAccessibleAsync(receipt.StudentId, receipt.FinanceFeeAccountId, cancellationToken);

        if (receipt.PdfBlobName != null && receipt.PdfBlobName.Contains("/v4/", StringComparison.Ordinal))
        {
            var cached = await blobManager.GetPdfOrNullAsync(receipt.PdfBlobName, cancellationToken);
            if (cached != null) return cached;
        }

        var student      = receipt.StudentId.HasValue ? await AsyncExecuter.FirstOrDefaultAsync((await LazyServiceProvider.LazyGetRequiredService<IStudentRepository>().GetQueryableWithoutAutoIncludesAsync()).Where(s => s.Id == receipt.StudentId.Value), cancellationToken) : null;
        var academicYear = await academicYearRepository.FindAsync(receipt.AcademicYearId, includeDetails: false, cancellationToken: cancellationToken);

        var pdfBytes = await pdfGenerator.GenerateAsync(receipt, student, academicYear, cancellationToken);
        var blobName = await blobManager.SavePdfAsync(receipt.Id, receipt.ReceiptKind, pdfBytes, cancellationToken);

        receipt.SetPdfBlobName(blobName);
        await receiptRepository.UpdateAsync(receipt, autoSave: true, cancellationToken: cancellationToken);
        return pdfBytes;
    }

    private static void ValidateListInput(GetFeeReceiptListInput input)
    {
        if (input.AcademicYearId == Guid.Empty)
            throw new UserFriendlyException(FeeReceiptErrorCodes.InvalidAcademicContext, "AcademicYearId is required.");
        if (input.BoardId == Guid.Empty || input.EducationMediumId == Guid.Empty)
            throw new UserFriendlyException(FeeReceiptErrorCodes.BoardMediumRequired, "BoardId and EducationMediumId are required.");
        if (input.Month is < 1 or > 12)
            throw new UserFriendlyException(FeeReceiptErrorCodes.InvalidAcademicContext, "Month must be between 1 and 12.");
    }

    public static string GetKindLabel(FeeReceiptKind kind) => kind switch
    {
        FeeReceiptKind.FinalFee          => "Receipt (Fee)",
        FeeReceiptKind.FinalCharge       => "Receipt (Charge)",
        FeeReceiptKind.ProvisionalFee    => "Provisional Receipt (Fee)",
        FeeReceiptKind.ProvisionalCharge => "Provisional Receipt (Charge)",
        _                               => kind.ToString(),
    };
}

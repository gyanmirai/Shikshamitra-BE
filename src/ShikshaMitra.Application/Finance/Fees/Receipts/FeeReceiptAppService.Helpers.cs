using System;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Receipts.Dtos;
using Volo.Abp;

namespace ShikshaMitra.Finance.Fees.Receipts;

public partial class FeeReceiptAppService
{
    public async Task<byte[]> GetPdfAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var receipt = await receiptRepository.GetAsync(id, cancellationToken: cancellationToken)
            ?? throw new UserFriendlyException(FeeReceiptErrorCodes.ReceiptNotFound, "Receipt not found.");
        await financeScopeAccessService.EnsureStudentAccessibleAsync(receipt.StudentId, cancellationToken);

        if (receipt.PdfBlobName != null)
        {
            var cached = await blobManager.GetPdfOrNullAsync(receipt.PdfBlobName, cancellationToken);
            if (cached != null) return cached;
        }

        var student      = await studentRepository.FindAsync(receipt.StudentId, cancellationToken: cancellationToken);
        var academicYear = await academicYearRepository.FindAsync(receipt.AcademicYearId, cancellationToken: cancellationToken);

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
        FeeReceiptKind.FinalFee          => "Final Receipt",
        FeeReceiptKind.ProvisionalFee    => "Kachi Receipt (Fee)",
        FeeReceiptKind.ProvisionalCharge => "Kachi Receipt (Charge)",
        _                               => kind.ToString(),
    };
}

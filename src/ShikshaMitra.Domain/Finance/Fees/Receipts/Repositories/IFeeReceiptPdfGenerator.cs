using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;

namespace ShikshaMitra.Finance.Fees.Receipts;

/// <summary>
/// Generates a PDF document for a given <see cref="FeePaymentReceipt"/>.
/// Implementations in the Application layer use QuestPDF with a per-kind template.
/// </summary>
public interface IFeeReceiptPdfGenerator
{
    /// <summary>
    /// Generates and returns the PDF bytes for the specified receipt.
    /// The correct template (Final / Kachi Fee / Kachi Charge) is selected
    /// based on <see cref="FeePaymentReceipt.ReceiptKind"/>.
    /// </summary>
    Task<byte[]> GenerateAsync(
        FeePaymentReceipt receipt,
        Student? student,
        AcademicYear? academicYear,
        CancellationToken cancellationToken = default);
}

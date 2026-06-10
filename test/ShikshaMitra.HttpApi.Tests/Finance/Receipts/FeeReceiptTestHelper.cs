using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.FeeCollections;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Finance.FeeReceipts;

/// <summary>
/// Test helper that creates <see cref="FeePaymentReceipt"/> rows directly in
/// the database (bypassing the collect flow, which is out of scope).
/// </summary>
public static class FeeReceiptTestHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Guid DefaultAcademicYearId { get; set; }
    public static Guid DefaultMediumId { get; set; }
    public static Guid DefaultBoardId { get; set; }

    /// <summary>
    /// Seeds a <see cref="FeePaymentReceipt"/> row directly through the domain repository.
    /// </summary>
    public static async Task<FeePaymentReceipt> SeedReceiptAsync(
        IServiceProvider services,
        Guid studentId,
        FeeReceiptKind kind = FeeReceiptKind.FinalFee,
        FeeReceiptScope scope = FeeReceiptScope.Fee,
        double amount = 1000d,
        Guid? feeCollectionTransactionId = null)
    {
        using var scope2 = services.CreateScope();
        var uowManager = scope2.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        using var uow = uowManager.Begin(requiresNew: true);

        var receiptRepo = scope2.ServiceProvider.GetRequiredService<IFeePaymentReceiptRepository>();

        var verificationStatus = kind == FeeReceiptKind.ProvisionalFee
            ? FeeReceiptVerificationStatus.PendingVerification
            : FeeReceiptVerificationStatus.NotApplicable;

        var prefix = kind switch
        {
            FeeReceiptKind.FinalFee          => "FIN",
            FeeReceiptKind.ProvisionalFee    => "KFE",
            FeeReceiptKind.ProvisionalCharge => "KCH",
            _                               => "REC",
        };

        var receiptNumber = $"{prefix}-TEST-{Guid.NewGuid():N}"[..20];

        var receipt = new FeePaymentReceipt(Guid.NewGuid())
        {
            TenantId                   = null,
            FeeCollectionTransactionId = feeCollectionTransactionId ?? Guid.NewGuid(),
            StudentId                  = studentId,
            AcademicYearId             = DefaultAcademicYearId,
            BoardId                    = DefaultBoardId,
            EducationMediumId          = DefaultMediumId,
            ReceiptScope               = scope,
            ReceiptKind                = kind,
            VerificationStatus         = verificationStatus,
            TotalPaidAmount            = amount,
            ReceiptNumber              = receiptNumber,
            ReceiptDate                = DateOnly.FromDateTime(DateTime.UtcNow),
        };

        await receiptRepo.InsertAsync(receipt, autoSave: true);
        await uow.CompleteAsync();

        return receipt;
    }
}

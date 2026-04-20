using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Charges;
using ShikshaMitra.Finance.Setting;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;
using PD = ShikshaMitra.Finance.FeeCollection.PaymentDetails;

namespace ShikshaMitra.Finance.FeeCollection;

[Authorize]
[RemoteService(false)]
public class FeeCollectionAppService(
    IRepository<FeeRecordMetadata, Guid> feeRecordMetadataRepository,
    IRepository<FeeCollectionRecord, Guid> feeCollectionRecordRepository,
    IRepository<ChargeCollectionRecord, Guid> chargeCollectionRecordRepository,
    IRepository<PaymentMethodRecord, Guid> paymentMethodRecordRepository,
    IRepository<FeeHead, Guid> feeHeadRepository,
    IRepository<Charge, Guid> chargeRepository,
    IRepository<AcademicYear, Guid> academicYearRepository,
    ICurrentUser currentUser
) : ApplicationService, IFeeCollectionAppService
{
    public Task<StudentFeeCollectionListDto> GetStudentsAsync(GetStudentsInput input)
    {
        throw new NotImplementedException();
    }

    [Authorize(ShikshaMitraSubModulePermissions.FeeCollection)]
    public async Task<CollectFeeOutput> CollectFeeAsync(CollectFeeInput input)
    {
        // ── 1. Validate referenced entities ──────────────────────────────
        await academicYearRepository.GetAsync(input.AcademicYearId);

        var feeHeadIds = input.SelectedFeeHeads.Select(f => f.FeeHeadId).Distinct().ToList();
        var feeHeads = await feeHeadRepository.GetListAsync(fh => feeHeadIds.Contains(fh.Id));

        if (feeHeads.Count != feeHeadIds.Count)
            throw new BusinessException(FeeCollectionErrorCodes.InvalidFeeHead);

        var chargeMap = new Dictionary<Guid, Charge>();
        if (input.SelectedCharges is { Count: > 0 })
        {
            var chargeIds = input.SelectedCharges.Select(c => c.ChargeId).Distinct().ToList();
            var charges = await chargeRepository.GetListAsync(c => chargeIds.Contains(c.Id));
            if (charges.Count != chargeIds.Count)
                throw new BusinessException(FeeCollectionErrorCodes.InvalidCharge);
            chargeMap = charges.ToDictionary(c => c.Id);
        }

        // ── 2. Determine status ───────────────────────────────────────────
        var received = input.ReceivedAmount ?? input.TotalAmount;
        var status = received >= input.TotalAmount
            ? FeeCollectionStatus.Paid
            : FeeCollectionStatus.Pending;

        // ── 3. Generate receipt number ────────────────────────────────────
        var receiptNumber = await GenerateReceiptNumberAsync();

        // ── 4. Create FeeRecordMetadata (root of this collection event) ──
        var now = DateTimeOffset.UtcNow;
        var metadata = new FeeRecordMetadata(GuidGenerator.Create())
        {
            TenantId = CurrentTenant.Id,
            StudentId = input.StudentId,
            AcademicYearId = input.AcademicYearId,
            TotalPaidFee = received,
            ReceiptNumber = receiptNumber,
            Status = status,
            Remarks = input.Notes,
        };
        await feeRecordMetadataRepository.InsertAsync(metadata, autoSave: false);

        // ── 5. Fee-head collection records ────────────────────────────────
        var feeHeadMap = feeHeads.ToDictionary(fh => fh.Id);
        var feeCollectionRecords = input.SelectedFeeHeads
            .Select(sfh => new FeeCollectionRecord(GuidGenerator.Create())
            {
                TenantId = CurrentTenant.Id,
                StudentId = input.StudentId,
                FeeHeadId = sfh.FeeHeadId,
                Amount = sfh.Amount,
                PaymentDate = now,
                FeeRecordMetadataId = metadata.Id,
            })
            .ToList();
        await feeCollectionRecordRepository.InsertManyAsync(feeCollectionRecords, autoSave: false);

        // ── 6. Charge collection records ──────────────────────────────────
        var chargeCollectionRecords = new List<ChargeCollectionRecord>();
        if (input.SelectedCharges is { Count: > 0 })
        {
            chargeCollectionRecords = input.SelectedCharges
                .Select(sc => new ChargeCollectionRecord(GuidGenerator.Create())
                {
                    TenantId = CurrentTenant.Id,
                    StudentId = input.StudentId,
                    ChargeId = sc.ChargeId,
                    Amount = sc.Amount,
                    PaymentDate = now,
                    FeeRecordMetadataId = metadata.Id,
                })
                .ToList();
            await chargeCollectionRecordRepository.InsertManyAsync(chargeCollectionRecords, autoSave: false);
        }

        // ── 7. Payment-method records ─────────────────────────────────────
        var paymentRecords = input.Payments
            .Select(p => PaymentMethodRecord.Create(
                studentId: input.StudentId,
                feeRecordMetadataId: metadata.Id,
                amount: (decimal)p.Amount,
                paymentDate: now,
                paymentMethod: p.PaymentMethod,
                paymentDetails: MapPaymentDetails(p.PaymentMethod, p.Details),
                tenantId: CurrentTenant.Id,
                notes: input.Notes))
            .ToList();
        await paymentMethodRecordRepository.InsertManyAsync(paymentRecords, autoSave: true);

        // ── 8. Build and return response ──────────────────────────────────
        var collectedBy = currentUser.Id ?? Guid.Empty;

        return new CollectFeeOutput
        {
            Id = metadata.Id,
            ReceiptNumber = receiptNumber,
            StudentId = input.StudentId,
            AcademicYearId = input.AcademicYearId,
            TotalAmount = input.TotalAmount,
            Status = status,
            CollectedBy = collectedBy,
            CollectedAt = now,
            CreatedAt = now,
            ReceiptUrl = null,
            PaidFeeHeads = feeCollectionRecords
                .Select(r => new PaidFeeHeadInfoDto
                {
                    FeeHeadId = r.FeeHeadId,
                    FeeHeadName = feeHeadMap[r.FeeHeadId].Name,
                    AmountPaid = r.Amount,
                })
                .ToList(),
            PaidCharges = chargeCollectionRecords.Count > 0
                ? chargeCollectionRecords
                    .Select(r => new PaidChargeInfoDto
                    {
                        ChargeId = r.ChargeId,
                        ChargeName = chargeMap[r.ChargeId].PurposeDescription,
                        AmountPaid = r.Amount,
                    })
                    .ToList()
                : null,
        };
    }

    public Task<StudentFeeDetailsDto> GetStudentFeeDetailsAsync(GetStudentFeeDetailsInput input)
    {
        throw new NotImplementedException();
    }

    public Task<ComprehensiveFeeDetailsDto> GetComprehensiveFeeDetailsAsync(GetComprehensiveFeeDetailsInput input)
    {
        throw new NotImplementedException();
    }

    // ── Private helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Generates a unique receipt number in the format REC-{YYYY}-{NNNNNN}.
    /// Uses the total count of existing metadata records to derive the sequence.
    /// </summary>
    private async Task<string> GenerateReceiptNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var count = await feeRecordMetadataRepository.CountAsync();
        var sequence = (count + 1).ToString("D6");
        return $"REC-{year}-{sequence}";
    }

    /// <summary>
    /// Maps the raw details dictionary from the API request into a strongly-typed
    /// <see cref="PD.PaymentDetails"/> value object matching the payment method.
    /// Returns <c>null</c> for Cash — no instrument details are required.
    /// </summary>
    private static PD.PaymentDetails? MapPaymentDetails(
        PaymentMethodEnum method,
        Dictionary<string, object?>? details)
    {
        if (method == PaymentMethodEnum.Cash)
            return null;

        details ??= [];

        string Get(string key) =>
            details.TryGetValue(key, out var v) ? v?.ToString() ?? string.Empty : string.Empty;

        return method switch
        {
            PaymentMethodEnum.Cheque => new PD.ChequeDetails(
                chequeNumber: Get("chequeNumber"),
                bankName: Get("bankName"),
                branchName: Get("branchName"),
                chequeDate: DateOnly.TryParse(Get("chequeDate"), out var cd)
                    ? cd : DateOnly.FromDateTime(DateTime.UtcNow)),

            PaymentMethodEnum.DemandDraft => new PD.DemandDraftDetails(
                ddNumber: Get("ddNumber"),
                issuingBankName: Get("issuingBankName"),
                branchName: Get("branchName"),
                ddDate: DateOnly.TryParse(Get("ddDate"), out var dd)
                    ? dd : DateOnly.FromDateTime(DateTime.UtcNow)),

            PaymentMethodEnum.BankTransfer => new PD.BankTransferDetails(
                transactionReferenceNumber: Get("transactionReferenceNumber"),
                bankName: Get("bankName"),
                ifscCode: Get("ifscCode")),

            PaymentMethodEnum.UPI => new PD.UpiDetails(
                upiTransactionId: Get("upiTransactionId"),
                upiId: Get("upiId"),
                appName: Get("appName")),

            // CreditCard, OnlinePaymentGateway, WalletPrepaidCard — no dedicated
            // details type yet. Domain validation in PaymentMethodRecord.Create()
            // will throw if details are unexpectedly required.
            _ => null,
        };
    }
}

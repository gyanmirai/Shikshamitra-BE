using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts.Dtos;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.Receipts;

[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_InvoiceReceipt.View)]
[RemoteService(false)]
public partial class FeeReceiptAppService(
    IFeePaymentReceiptRepository receiptRepository,
    FeeReceiptBlobManager blobManager,
    IFeeReceiptPdfGenerator pdfGenerator,
    FinanceScopeAccessService financeScopeAccessService,
    IReadOnlyRepository<Student, Guid> studentRepository,
    IReadOnlyRepository<AcademicYear, Guid> academicYearRepository,
    IReadOnlyRepository<EducationBoard, Guid> boardRepository,
    IReadOnlyRepository<EducationMedium, Guid> mediumRepository,
    IReadOnlyRepository<Standard, Guid> standardRepository,
    IReadOnlyRepository<Division, Guid> divisionRepository,
    IRepository<FeeCollectionDueFeeAllocation, Guid> dueFeeAllocationRepository,
    IDueFeeRepository dueFeeRepository
) : ApplicationService, IFeeReceiptAppService
{
    public async Task<PagedResultDto<FeeReceiptListItemDto>> GetListAsync(
        GetFeeReceiptListInput input,
        CancellationToken cancellationToken = default)
    {
        ValidateListInput(input);
        FinanceClassDivisionFilterValidator.Validate(input);

        var scopedStudentIds = await financeScopeAccessService.GetScopedStudentIdsQueryAsync();
        var filter = new FeeReceiptListFilter
        {
            AcademicYearId        = input.AcademicYearId,
            BoardId               = input.BoardId,
            EducationMediumId     = input.EducationMediumId,
            Month                 = input.Month,
            ReceiptKind           = input.ReceiptKind,
            Search                = input.Search,
            StandardId            = input.StandardId,
            DivisionId            = input.DivisionId,
            SkipCount             = input.SkipCount,
            MaxResultCount        = input.MaxResultCount,
            ScopedStudentIdsQuery = scopedStudentIds,
            ScopedAccountIdsQuery = await financeScopeAccessService.GetScopedFeeAccountIdsQueryAsync(),
        };

        var (totalCount, receipts) = await receiptRepository.GetPagedListAsync(filter, cancellationToken);
        if (totalCount == 0)
            return new PagedResultDto<FeeReceiptListItemDto>(0, []);

        var studentIds = receipts.Where(r => r.StudentId.HasValue).Select(r => r.StudentId!.Value).Distinct().ToList();
        var students   = await AsyncExecuter.ToListAsync((await LazyServiceProvider.LazyGetRequiredService<IStudentRepository>().GetQueryableWithoutAutoIncludesAsync()).Where(s => Enumerable.Contains(studentIds, s.Id)), cancellationToken);
        var studentMap = students.ToDictionary(s => s.Id);
        var accountIds = receipts.Where(r => r.FinanceFeeAccountId.HasValue).Select(r => r.FinanceFeeAccountId!.Value).Distinct().ToList();
        var accountRepository = LazyServiceProvider.LazyGetRequiredService<IReadOnlyRepository<FinanceFeeAccount, Guid>>();
        var accountMap = (await accountRepository.GetListAsync(a => accountIds.Contains(a.Id), cancellationToken: cancellationToken)).ToDictionary(a => a.Id);

        var linkedFinalIds = receipts
            .Where(r => r.LinkedFinalReceiptId.HasValue)
            .Select(r => r.LinkedFinalReceiptId!.Value).Distinct().ToList();
        var linkedFinals   = linkedFinalIds.Count > 0
            ? await receiptRepository.GetListAsync(r => linkedFinalIds.Contains(r.Id))
            : [];
        var linkedFinalMap = linkedFinals.ToDictionary(r => r.Id, r => r.ReceiptNumber);
        var transactionIds = receipts.Select(r => r.FeeCollectionTransactionId).Distinct().ToList();
        var payments = await LazyServiceProvider.LazyGetRequiredService<IReadOnlyRepository<PaymentMethodRecord, Guid>>()
            .GetListAsync(p => transactionIds.Contains(p.FeeRecordMetadataId), cancellationToken: cancellationToken);
        var methodLabels = payments.GroupBy(p => p.FeeRecordMetadataId).ToDictionary(g => g.Key,
            g => string.Join(", ", g.Select(p => p.PaymentMethod).Distinct().OrderBy(m => m)
                .Select(FeeReceiptPresentation.GetPaymentMethodLabel)));

        var items = receipts.Select(r =>
        {
            var student = r.StudentId.HasValue ? studentMap.GetValueOrDefault(r.StudentId.Value) : null;
            var account = r.FinanceFeeAccountId.HasValue ? accountMap.GetValueOrDefault(r.FinanceFeeAccountId.Value) : null;
            return new FeeReceiptListItemDto
            {
                Id                         = r.Id,
                ReceiptNumber              = r.ReceiptNumber,
                ReceiptKind                = r.ReceiptKind,
                ReceiptKindLabel           = FeeReceiptPresentation.GetLabel(r.ReceiptKind, r.ReceiptScope),
                PaymentMethodLabel         = methodLabels.GetValueOrDefault(r.FeeCollectionTransactionId, "Not recorded"),
                PdfFileName                = FeeReceiptPresentation.GetPdfFileName(r.ReceiptKind, r.ReceiptScope, r.ReceiptNumber, r.ReceiptDate),
                ReceiptScope               = r.ReceiptScope,
                VerificationStatus         = r.VerificationStatus,
                LinkedFinalReceiptNumber   = r.LinkedFinalReceiptId.HasValue
                    ? linkedFinalMap.GetValueOrDefault(r.LinkedFinalReceiptId.Value)
                    : null,
                TotalPaidAmount            = r.TotalPaidAmount,
                StudentId                  = r.StudentId,
                FinanceFeeAccountId        = r.FinanceFeeAccountId,
                StudentName                = student != null
                    ? $"{student.FirstName} {student.MiddleName} {student.LastName}".Trim().Replace("  ", " ")
                    : account?.ApplicantName ?? string.Empty,
                GrNo                       = student?.GrNo,
                RollNo                     = student?.RollNo?.ToString(),
                ReceiptDate                = r.ReceiptDate,
                CreationTime               = r.CreationTime,
                HasPdf                     = r.PdfBlobName != null,
                FeeCollectionTransactionId = r.FeeCollectionTransactionId,
            };
        }).ToList();

        return new PagedResultDto<FeeReceiptListItemDto>(totalCount, items);
    }

    public async Task<FeeReceiptDetailDto> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var receipt = await receiptRepository.GetAsync(id, cancellationToken: cancellationToken)
            ?? throw new UserFriendlyException(FeeReceiptErrorCodes.ReceiptNotFound, "Receipt not found.");
        await financeScopeAccessService.EnsureCollectionPayerAccessibleAsync(receipt.StudentId, receipt.FinanceFeeAccountId, cancellationToken);

        var student      = receipt.StudentId.HasValue ? await AsyncExecuter.FirstOrDefaultAsync((await LazyServiceProvider.LazyGetRequiredService<IStudentRepository>().GetQueryableWithoutAutoIncludesAsync()).Where(s => s.Id == receipt.StudentId.Value), cancellationToken) : null;
        var account = receipt.FinanceFeeAccountId.HasValue
            ? await LazyServiceProvider.LazyGetRequiredService<IReadOnlyRepository<FinanceFeeAccount, Guid>>().GetAsync(receipt.FinanceFeeAccountId.Value, cancellationToken: cancellationToken)
            : null;
        var academicYear = await academicYearRepository.FindAsync(receipt.AcademicYearId, includeDetails: false, cancellationToken: cancellationToken);
        var board        = await AsyncExecuter.FirstOrDefaultAsync((await boardRepository.GetQueryableAsync()).Where(b => b.Id == receipt.BoardId).Select(b => new { b.Type }), cancellationToken);
        var medium       = await AsyncExecuter.FirstOrDefaultAsync((await mediumRepository.GetQueryableAsync()).Where(m => m.Id == receipt.EducationMediumId).Select(m => new { m.Name }), cancellationToken);

        var siblings = await receiptRepository.GetListAsync(
            r => r.FeeCollectionTransactionId == receipt.FeeCollectionTransactionId && r.Id != receipt.Id,
            cancellationToken: cancellationToken);

        FeePaymentReceipt? linkedProvisional = null;
        FeePaymentReceipt? linkedFinal       = null;
        if (receipt.LinkedProvisionalReceiptId.HasValue)
            linkedProvisional = await receiptRepository.FindAsync(
                receipt.LinkedProvisionalReceiptId.Value, cancellationToken: cancellationToken);
        if (receipt.LinkedFinalReceiptId.HasValue)
            linkedFinal = await receiptRepository.FindAsync(
                receipt.LinkedFinalReceiptId.Value, cancellationToken: cancellationToken);

        string? className = null;
        string? division  = null;
        var standardId = account?.StandardId ?? student?.StandardId;
        var divisionId = account?.DivisionId ?? student?.DivisionId;
        if (standardId.HasValue)
        {
            var std = await AsyncExecuter.FirstOrDefaultAsync((await standardRepository.GetQueryableAsync()).Where(s => s.Id == standardId.Value).Select(s => new { s.Name }), cancellationToken);
            className = std?.Name;
        }
        if (divisionId.HasValue)
        {
            var div = await AsyncExecuter.FirstOrDefaultAsync((await divisionRepository.GetQueryableAsync()).Where(d => d.Id == divisionId.Value).Select(d => new { d.Name }), cancellationToken);
            division = div?.Name;
        }

        var allocationDetails = new List<FeeReceiptAllocationLineDto>();
        var paidFeeHeads = new List<FeeReceiptPaidHeadDto>();

        if (receipt.ReceiptScope is FeeReceiptScope.Fee or FeeReceiptScope.Combined)
        {
            var allocations = await dueFeeAllocationRepository.GetListAsync(
                a => a.FeeRecordMetadataId == receipt.FeeCollectionTransactionId,
                cancellationToken: cancellationToken);

            var dueFeeIds = allocations.Select(a => a.DueFeeId).Distinct().ToList();
            var dueFees = dueFeeIds.Count == 0
                ? new List<DueFee>()
                : await dueFeeRepository.GetListAsync(d => dueFeeIds.Contains(d.Id), cancellationToken: cancellationToken);
            var dueFeeMap = dueFees.ToDictionary(d => d.Id);

            allocationDetails = allocations.Select(a =>
            {
                dueFeeMap.TryGetValue(a.DueFeeId, out var dueFee);
                return new FeeReceiptAllocationLineDto
                {
                    DueFeeId = a.DueFeeId,
                    FeeHeadId = dueFee?.FeeHeadId,
                    FeeHeadName = a.FeeHeadName ?? string.Empty,
                    InstallmentLabel = a.InstallmentLabel
                        ?? dueFee?.GetDisplayInstallmentLabel()
                        ?? string.Empty,
                    CoverageStartDate = a.CoverageStartDate ?? dueFee?.CoverageStartDate,
                    CoverageEndDate = a.CoverageEndDate ?? dueFee?.CoverageEndDate,
                    DueDate = a.DueDate ?? dueFee?.ScheduleDueDate,
                    AmountPaid = a.Amount,
                    ScheduleSource = (a.InstallmentLabel != null || dueFee != null)
                        ? (a.ScheduleSource != default
                            ? a.ScheduleSource.ToString()
                            : dueFee?.ScheduleSource.ToString() ?? "Legacy")
                        : "Legacy",
                };
            }).ToList();

            paidFeeHeads = allocationDetails
                .GroupBy(a => new { a.FeeHeadId, a.FeeHeadName })
                .Select(g => new FeeReceiptPaidHeadDto
                {
                    FeeHeadId = g.Key.FeeHeadId ?? Guid.Empty,
                    FeeHeadName = g.Key.FeeHeadName,
                    AmountPaid = g.Sum(x => x.AmountPaid),
                }).ToList();
        }

        var receiptPayments = await LazyServiceProvider.LazyGetRequiredService<IReadOnlyRepository<PaymentMethodRecord, Guid>>()
            .GetListAsync(p => p.FeeRecordMetadataId == receipt.FeeCollectionTransactionId, cancellationToken: cancellationToken);
        return new FeeReceiptDetailDto
        {
            Id                             = receipt.Id,
            ReceiptNumber                  = receipt.ReceiptNumber,
            ReceiptKind                    = receipt.ReceiptKind,
            ReceiptKindLabel               = FeeReceiptPresentation.GetLabel(receipt.ReceiptKind, receipt.ReceiptScope),
            PaymentMethodLabel             = receiptPayments.Count == 0 ? "Not recorded" : string.Join(", ", receiptPayments.Select(p => p.PaymentMethod).Distinct().OrderBy(m => m).Select(FeeReceiptPresentation.GetPaymentMethodLabel)),
            PdfFileName                    = FeeReceiptPresentation.GetPdfFileName(receipt.ReceiptKind, receipt.ReceiptScope, receipt.ReceiptNumber, receipt.ReceiptDate),
            ReceiptScope                   = receipt.ReceiptScope,
            VerificationStatus             = receipt.VerificationStatus,
            LinkedProvisionalReceiptId     = receipt.LinkedProvisionalReceiptId,
            LinkedProvisionalReceiptNumber = linkedProvisional?.ReceiptNumber,
            LinkedFinalReceiptId           = receipt.LinkedFinalReceiptId,
            LinkedFinalReceiptNumber       = linkedFinal?.ReceiptNumber,
            TotalPaidAmount                = receipt.TotalPaidAmount,
			FeePaidAmount = receipt.FeePaidAmount, ChargePaidAmount = receipt.ChargePaidAmount,
            StudentId                      = receipt.StudentId,
            FinanceFeeAccountId            = receipt.FinanceFeeAccountId,
            StudentName                    = student != null
                ? $"{student.FirstName} {student.MiddleName} {student.LastName}".Trim().Replace("  ", " ")
                : account?.ApplicantName ?? string.Empty,
            GrNo                           = student?.GrNo,
            RollNo                         = student?.RollNo?.ToString(),
            ClassName                      = className,
            Division                       = division,
            AcademicYearId                 = receipt.AcademicYearId,
            BoardId                        = receipt.BoardId,
            EducationMediumId              = receipt.EducationMediumId,
            AcademicYearName               = academicYear?.Name,
            AcademicYearStartDate          = academicYear?.StartDate,
            AcademicYearEndDate            = academicYear?.EndDate,
            BoardName                      = board?.Type.ToString(),
            MediumName                     = medium?.Name,
            ReceiptDate                    = receipt.ReceiptDate,
            CreationTime                   = receipt.CreationTime,
            HasPdf                         = receipt.PdfBlobName != null,
            FeeCollectionTransactionId     = receipt.FeeCollectionTransactionId,
            SiblingReceiptNumber           = siblings.FirstOrDefault()?.ReceiptNumber,
            Remarks                        = receipt.Remarks,
            PaidFeeHeads                   = paidFeeHeads,
            AllocationDetails              = allocationDetails,
        };
    }
}

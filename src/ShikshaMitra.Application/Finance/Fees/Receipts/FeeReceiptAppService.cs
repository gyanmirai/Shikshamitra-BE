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
            Month                 = input.Month+1,
            ReceiptKind           = input.ReceiptKind,
            Search                = input.Search,
            StandardId            = input.StandardId,
            DivisionId            = input.DivisionId,
            SkipCount             = input.SkipCount,
            MaxResultCount        = input.MaxResultCount,
            ScopedStudentIdsQuery = scopedStudentIds,
        };

        var (totalCount, receipts) = await receiptRepository.GetPagedListAsync(filter, cancellationToken);
        if (totalCount == 0)
            return new PagedResultDto<FeeReceiptListItemDto>(0, []);

        var studentIds = receipts.Select(r => r.StudentId).Distinct().ToList();
        var students   = await studentRepository.GetListAsync(s => studentIds.Contains(s.Id));
        var studentMap = students.ToDictionary(s => s.Id);

        var linkedFinalIds = receipts
            .Where(r => r.LinkedFinalReceiptId.HasValue)
            .Select(r => r.LinkedFinalReceiptId!.Value).Distinct().ToList();
        var linkedFinals   = linkedFinalIds.Count > 0
            ? await receiptRepository.GetListAsync(r => linkedFinalIds.Contains(r.Id))
            : [];
        var linkedFinalMap = linkedFinals.ToDictionary(r => r.Id, r => r.ReceiptNumber);

        var items = receipts.Select(r =>
        {
            studentMap.TryGetValue(r.StudentId, out var student);
            return new FeeReceiptListItemDto
            {
                Id                         = r.Id,
                ReceiptNumber              = r.ReceiptNumber,
                ReceiptKind                = r.ReceiptKind,
                ReceiptKindLabel           = GetKindLabel(r.ReceiptKind),
                ReceiptScope               = r.ReceiptScope,
                VerificationStatus         = r.VerificationStatus,
                LinkedFinalReceiptNumber   = r.LinkedFinalReceiptId.HasValue
                    ? linkedFinalMap.GetValueOrDefault(r.LinkedFinalReceiptId.Value)
                    : null,
                TotalPaidAmount            = r.TotalPaidAmount,
                StudentId                  = r.StudentId,
                StudentName                = student != null
                    ? $"{student.FirstName} {student.MiddleName} {student.LastName}".Trim().Replace("  ", " ")
                    : string.Empty,
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
        await financeScopeAccessService.EnsureStudentAccessibleAsync(receipt.StudentId, cancellationToken);

        var student      = await studentRepository.FindAsync(receipt.StudentId, cancellationToken: cancellationToken);
        var academicYear = await academicYearRepository.FindAsync(receipt.AcademicYearId, cancellationToken: cancellationToken);
        var board        = await boardRepository.FindAsync(receipt.BoardId, cancellationToken: cancellationToken);
        var medium       = await mediumRepository.FindAsync(receipt.EducationMediumId, cancellationToken: cancellationToken);

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
        if (student?.StandardId.HasValue == true)
        {
            var std = await standardRepository.FindAsync(student.StandardId!.Value, cancellationToken: cancellationToken);
            className = std?.Name;
        }
        if (student?.DivisionId.HasValue == true)
        {
            var div = await divisionRepository.FindAsync(student.DivisionId!.Value, cancellationToken: cancellationToken);
            division = div?.Name;
        }

        var allocationDetails = new List<FeeReceiptAllocationLineDto>();
        var paidFeeHeads = new List<FeeReceiptPaidHeadDto>();

        if (receipt.ReceiptScope == FeeReceiptScope.Fee)
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

        return new FeeReceiptDetailDto
        {
            Id                             = receipt.Id,
            ReceiptNumber                  = receipt.ReceiptNumber,
            ReceiptKind                    = receipt.ReceiptKind,
            ReceiptKindLabel               = GetKindLabel(receipt.ReceiptKind),
            ReceiptScope                   = receipt.ReceiptScope,
            VerificationStatus             = receipt.VerificationStatus,
            LinkedProvisionalReceiptId     = receipt.LinkedProvisionalReceiptId,
            LinkedProvisionalReceiptNumber = linkedProvisional?.ReceiptNumber,
            LinkedFinalReceiptId           = receipt.LinkedFinalReceiptId,
            LinkedFinalReceiptNumber       = linkedFinal?.ReceiptNumber,
            TotalPaidAmount                = receipt.TotalPaidAmount,
            StudentId                      = receipt.StudentId,
            StudentName                    = student != null
                ? $"{student.FirstName} {student.MiddleName} {student.LastName}".Trim().Replace("  ", " ")
                : string.Empty,
            GrNo                           = student?.GrNo,
            RollNo                         = student?.RollNo?.ToString(),
            ClassName                      = className,
            Division                       = division,
            AcademicYearId                 = receipt.AcademicYearId,
            BoardId                        = receipt.BoardId,
            EducationMediumId              = receipt.EducationMediumId,
            AcademicYearName               = academicYear?.Name,
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

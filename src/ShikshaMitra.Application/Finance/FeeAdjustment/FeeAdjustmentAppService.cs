using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Collections;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Finance.FeeAdjustment.Aggregates;
using ShikshaMitra.Finance.FeeAdjustment.Dtos;
using ShikshaMitra.Finance.FeeAdjustment.Repositories;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Uow;
using Volo.Abp.Users;

namespace ShikshaMitra.Finance.FeeAdjustment;

[Authorize]
[RemoteService(false)]
public class FeeAdjustmentAppService(
    IFeeAdjustmentRepository feeAdjustmentRepository,
    FeeAdjustmentManager feeAdjustmentManager,
    IDueFeeRepository dueFeeRepository,
    IRepository<Student, Guid> studentRepository,
    IRepository<FeeHead, Guid> feeHeadRepository,
    IRepository<Standard, Guid> standardRepository,
    IRepository<Division, Guid> divisionRepository,
    IRepository<IdentityUser, Guid> identityUserRepository,
    FinanceAuditWriter financeAuditWriter,
    ICurrentUser currentUser,
    IUnitOfWorkManager unitOfWorkManager) : ApplicationService, IFeeAdjustmentAppService
{
    private static readonly string[] MonthNames =
    [
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December"
    ];

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.View)]
    public async Task<AdjustmentEligibilityDto> GetEligibilityAsync(Guid studentId, Guid academicYearId)
    {
        var dueFees = await dueFeeRepository.GetListAsync(
            df => df.StudentId == studentId && df.PaidAmount > 0);

        var pendingSource = await feeAdjustmentRepository.GetPendingSourceAmountsByDueFeeAsync(studentId);

        if (dueFees.Count == 0)
        {
            return new AdjustmentEligibilityDto { StudentId = studentId, AcademicYearId = academicYearId };
        }

        var feeHeadIds = dueFees.Select(df => df.FeeHeadId).Distinct().ToList();
        var feeHeads = await feeHeadRepository.GetListAsync(fh => feeHeadIds.Contains(fh.Id));
        var feeHeadMap = feeHeads.ToDictionary(fh => fh.Id, fh => fh.Name);

        var grouped = dueFees
            .GroupBy(df => df.FeeHeadId)
            .Select(g =>
            {
                var months = g
                    .OrderBy(df => df.Year)
                    .ThenBy(df => df.Month)
                    .Select(df =>
                    {
                        pendingSource.TryGetValue(df.Id, out var reserved);
                        var adjustable = Math.Max(0, df.AdjustableBalance - reserved);
                        return new AdjustmentEligibilityMonthDto
                        {
                            DueFeeId = df.Id,
                            Month = df.Month,
                            Year = df.Year,
                            Total = df.OriginalAmount,
                            Paid = df.PaidAmount,
                            Refunded = df.RefundedAmount,
                            Adjusted = df.AdjustmentCreditAmount,
                            Adjustable = adjustable,
                        };
                    })
                    .Where(m => m.Adjustable > 0.001)
                    .ToList();

                return new AdjustmentEligibilityFeeHeadDto
                {
                    FeeHeadId = g.Key,
                    FeeHeadName = feeHeadMap.GetValueOrDefault(g.Key, g.Key.ToString()),
                    TotalPaid = g.Sum(df => df.PaidAmount),
                    TotalAdjustable = months.Sum(m => m.Adjustable),
                    Months = months,
                };
            })
            .Where(fh => fh.Months.Count > 0)
            .ToList();

        return new AdjustmentEligibilityDto
        {
            StudentId = studentId,
            AcademicYearId = academicYearId,
            TotalPaid = grouped.Sum(f => f.TotalPaid),
            TotalAdjustable = grouped.Sum(f => f.TotalAdjustable),
            TotalAlreadyAdjusted = dueFees.Sum(df => df.AdjustmentCreditAmount),
            FeeHeads = grouped,
        };
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Add)]
    public async Task<FeeAdjustmentDetailDto> CreateAsync(Guid academicYearId, CreateFeeAdjustmentDto input)
    {
        using var uow = unitOfWorkManager.Begin(isTransactional: true);

        var lines = input.SourceLines.Select(l => (l.DueFeeId, l.Amount)).ToList();
        var request = await feeAdjustmentManager.CreateAsync(
            academicYearId,
            input.StudentId,
            input.AdjustmentType,
            input.Reason,
            input.Remarks,
            lines);

        await feeAdjustmentRepository.InsertAsync(request, autoSave: false);

        var student = await studentRepository.GetAsync(input.StudentId);
        await financeAuditWriter.WriteAsync(
            academicYearId,
            FinanceAuditEntityTypes.FeeAdjustment,
            request.Id,
            request.VoucherNumber,
            FinanceAuditActions.Adjustment,
            $"Fee adjustment request {request.VoucherNumber} created.",
            BuildStudentContext(student, request.VoucherNumber),
            input.Reason);

        await uow.CompleteAsync();

        return await MapDetailAsync(request);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Add)]
    public async Task<FeeAdjustmentDetailDto> UpdateAsync(Guid id, Guid academicYearId, UpdateFeeAdjustmentDto input)
    {
        var existing = await feeAdjustmentRepository.GetWithDetailsAsync(id);
        existing.EnsurePending();
        await feeAdjustmentRepository.DeleteAsync(existing, autoSave: true);

        return await CreateAsync(academicYearId, input);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var request = await feeAdjustmentRepository.GetAsync(id);
        request.EnsurePending();
        await feeAdjustmentRepository.DeleteAsync(request);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.View)]
    public async Task<FeeAdjustmentDetailDto> GetAsync(Guid id)
    {
        var request = await feeAdjustmentRepository.GetWithDetailsAsync(id);
        return await MapDetailAsync(request);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.View)]
    public async Task<FeeAdjustmentApplicationsDto> GetApplicationsAsync(GetFeeAdjustmentApplicationsInput input)
    {
        var pageSize = Math.Min(Math.Max(input.PageSize, 1), 100);
        var skip = (Math.Max(input.Page, 1) - 1) * pageSize;

        var counts = await feeAdjustmentRepository.GetStatusCountsAsync(
            input.BoardId, input.EducationMediumId, input.AcademicYearId);

        var items = await feeAdjustmentRepository.GetListForApplicationsAsync(
            input.BoardId,
            input.EducationMediumId,
            input.AcademicYearId,
            input.Status,
            input.CompletedOnly,
            input.Filter,
            skip,
            pageSize);

        var total = await feeAdjustmentRepository.CountForApplicationsAsync(
            input.BoardId,
            input.EducationMediumId,
            input.AcademicYearId,
            input.Status,
            input.CompletedOnly,
            input.Filter);

        var studentIds = items.Select(i => i.StudentId).Distinct().ToList();
        var students = studentIds.Count > 0
            ? await studentRepository.GetListAsync(s => studentIds.Contains(s.Id))
            : [];
        var studentMap = students.ToDictionary(s => s.Id);

        var standardIds = students.Where(s => s.StandardId.HasValue).Select(s => s.StandardId!.Value).Distinct().ToList();
        var divisionIds = students.Where(s => s.DivisionId.HasValue).Select(s => s.DivisionId!.Value).Distinct().ToList();
        var standards = standardIds.Count > 0
            ? await standardRepository.GetListAsync(s => standardIds.Contains(s.Id))
            : [];
        var divisions = divisionIds.Count > 0
            ? await divisionRepository.GetListAsync(d => divisionIds.Contains(d.Id))
            : [];
        var standardMap = standards.ToDictionary(s => s.Id, s => s.Name);
        var divisionMap = divisions.ToDictionary(d => d.Id, d => d.Name);

        var userIds = items
            .Where(i => i.CreatorId.HasValue)
            .Select(i => i.CreatorId!.Value)
            .Distinct()
            .ToList();
        var users = userIds.Count > 0
            ? await identityUserRepository.GetListAsync(u => userIds.Contains(u.Id))
            : [];
        var userMap = users.ToDictionary(u => u.Id, u => u.Name ?? u.UserName ?? string.Empty);

        return new FeeAdjustmentApplicationsDto
        {
            PendingCount = counts.GetValueOrDefault(FeeAdjustmentStatus.Pending),
            PartiallyAppliedCount = counts.GetValueOrDefault(FeeAdjustmentStatus.PartiallyApplied),
            AppliedCount = counts.GetValueOrDefault(FeeAdjustmentStatus.Applied),
            RejectedCount = counts.GetValueOrDefault(FeeAdjustmentStatus.Rejected),
            CancelledCount = counts.GetValueOrDefault(FeeAdjustmentStatus.Cancelled),
            TotalCount = total,
            CurrentPage = input.Page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize),
            Items = items.Select(r =>
            {
                studentMap.TryGetValue(r.StudentId, out var student);
                return new FeeAdjustmentApplicationItemDto
                {
                    Id = r.Id,
                    StudentId = r.StudentId,
                    StudentName = FormatStudentName(student),
                    GrNumber = student?.GrNo?.ToString() ?? string.Empty,
                    RollNo = student?.RollNo?.ToString() ?? string.Empty,
                    StandardName = student?.StandardId is Guid sid && standardMap.TryGetValue(sid, out var sn) ? sn : string.Empty,
                    DivisionName = student?.DivisionId is Guid did && divisionMap.TryGetValue(did, out var dn) ? dn : string.Empty,
                    AdjustmentType = r.AdjustmentType,
                    AdjustmentTypeName = FormatAdjustmentType(r.AdjustmentType),
                    RequestedAmount = r.RequestedAmount,
                    AppliedAmount = r.AppliedAmount,
                    RemainingAmount = r.RemainingAmount,
                    Status = r.Status,
                    StatusName = FormatStatus(r.Status),
                    RequestedByName = r.CreatorId.HasValue && userMap.TryGetValue(r.CreatorId.Value, out var un) ? un : string.Empty,
                    CreatedAt = r.CreationTime,
                };
            }).ToList(),
        };
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Add)]
    public async Task<FeeAdjustmentApplyContextDto> GetApplyContextAsync(Guid id)
    {
        var request = await feeAdjustmentRepository.GetWithDetailsAsync(id);
        request.EnsureCanApply();

        var dueFees = await dueFeeRepository.GetListAsync(
            df => df.StudentId == request.StudentId && df.OutstandingAmount > 0.001);

        var feeHeadIds = dueFees.Select(df => df.FeeHeadId).Distinct().ToList();
        var feeHeads = feeHeadIds.Count > 0
            ? await feeHeadRepository.GetListAsync(fh => feeHeadIds.Contains(fh.Id))
            : [];
        var feeHeadMap = feeHeads.ToDictionary(fh => fh.Id, fh => fh.Name);

        var pendingFees = dueFees
            .OrderBy(df => df.Year)
            .ThenBy(df => df.Month)
            .Select(df => new FeeAdjustmentPendingFeeDto
            {
                InstallmentId = df.Id,
                DueFeeId = df.Id,
                InstallmentLabel = FormatMonthLabel(df.Month, df.Year),
                MonthOrder = (int)df.Month,
                FeeHeadId = df.FeeHeadId,
                FeeHeadName = feeHeadMap.GetValueOrDefault(df.FeeHeadId, string.Empty),
                TotalFees = df.OriginalAmount,
                PaidAmount = df.PaidAmount,
                PendingAmount = df.OutstandingAmount,
                DueAmount = df.OutstandingAmount,
                MaxApplicableAmount = Math.Min(df.OutstandingAmount, request.RemainingAmount),
            })
            .ToList();

        return new FeeAdjustmentApplyContextDto
        {
            AdjustmentId = request.Id,
            StudentId = request.StudentId,
            ApprovedAmount = request.RequestedAmount,
            RemainingAmount = request.RemainingAmount,
            TotalPendingFees = pendingFees.Sum(p => p.PendingAmount),
            PendingFees = pendingFees,
        };
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Add)]
    public async Task<ApplyFeeAdjustmentResultDto> ApplyAsync(
        Guid id,
        ApplyFeeAdjustmentDto input,
        string? idempotencyKey)
    {
        var approverId = currentUser.Id
            ?? throw new BusinessException(FeeAdjustmentErrorCodes.CannotApply);

        var request = await feeAdjustmentRepository.GetWithDetailsAsync(id);

        if (!string.IsNullOrWhiteSpace(idempotencyKey) && request.HasIdempotencyKey(idempotencyKey))
        {
            return await MapApplyResultAsync(request, approverId, 0);
        }

        if (!input.AllowPartialApply)
        {
            var batchTotal = input.Lines.Sum(l => l.AppliedAmount);
            if (Math.Abs(batchTotal - request.RemainingAmount) > 0.01)
            {
                throw new BusinessException(FeeAdjustmentErrorCodes.ApplyExceedsRemaining)
                    .WithData("Message", "Full apply required when partial apply is disabled.");
            }
        }

        using var uow = unitOfWorkManager.Begin(isTransactional: true);

        var targetLines = input.Lines.Select(l => (l.DueFeeId, l.AppliedAmount)).ToList();
        var batchBefore = request.AppliedAmount;

        await feeAdjustmentManager.ApplyAsync(request, approverId, input.ApprovalNotes, targetLines);

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            request.SetIdempotencyKey(idempotencyKey);
        }

        await feeAdjustmentRepository.UpdateAsync(request, autoSave: true);

        var student = await studentRepository.GetAsync(request.StudentId);
        await financeAuditWriter.WriteAsync(
            request.AcademicYearId,
            FinanceAuditEntityTypes.FeeAdjustment,
            request.Id,
            request.VoucherNumber,
            FinanceAuditActions.Approve,
            $"Applied {request.AppliedAmount - batchBefore:F2} from adjustment {request.VoucherNumber}.",
            BuildStudentContext(student, request.VoucherNumber),
            input.ApprovalNotes,
            context: input.Lines);

        await uow.CompleteAsync();

        return await MapApplyResultAsync(request, approverId, request.AppliedAmount - batchBefore);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Add)]
    public async Task<RejectFeeAdjustmentResultDto> RejectAsync(Guid id, RejectFeeAdjustmentDto input)
    {
        var reviewerId = currentUser.Id
            ?? throw new BusinessException(FeeAdjustmentErrorCodes.CannotApply);

        using var uow = unitOfWorkManager.Begin(isTransactional: true);

        var request = await feeAdjustmentRepository.GetWithDetailsAsync(id);
        feeAdjustmentManager.Reject(request, reviewerId, input.Message);
        await feeAdjustmentRepository.UpdateAsync(request, autoSave: true);

        var student = await studentRepository.GetAsync(request.StudentId);
        await financeAuditWriter.WriteAsync(
            request.AcademicYearId,
            FinanceAuditEntityTypes.FeeAdjustment,
            request.Id,
            request.VoucherNumber,
            FinanceAuditActions.Reject,
            $"Adjustment {request.VoucherNumber} rejected.",
            BuildStudentContext(student, request.VoucherNumber),
            input.Message);

        await uow.CompleteAsync();

        return new RejectFeeAdjustmentResultDto
        {
            AdjustmentId = request.Id,
            Status = request.Status,
            StatusName = FormatStatus(request.Status),
            RejectedAt = request.ProcessedAt ?? Clock.Now,
            RejectionReason = request.RejectionReason ?? input.Message,
        };
    }

    private async Task<FeeAdjustmentDetailDto> MapDetailAsync(FeeAdjustmentRequest request)
    {
        var student = await studentRepository.GetAsync(request.StudentId);
        var standard = student.StandardId.HasValue
            ? await standardRepository.FindAsync(student.StandardId.Value)
            : null;
        var division = student.DivisionId.HasValue
            ? await divisionRepository.FindAsync(student.DivisionId.Value)
            : null;

        var dueFeeIds = request.SourceLines.Select(l => l.DueFeeId)
            .Concat(request.TargetAllocations.Select(a => a.DueFeeId))
            .Distinct()
            .ToList();
        var dueFees = dueFeeIds.Count > 0
            ? await dueFeeRepository.GetListAsync(df => dueFeeIds.Contains(df.Id))
            : [];
        var dueFeeMap = dueFees.ToDictionary(df => df.Id);

        var feeHeadIds = dueFees.Select(df => df.FeeHeadId).Distinct().ToList();
        var feeHeads = feeHeadIds.Count > 0
            ? await feeHeadRepository.GetListAsync(fh => feeHeadIds.Contains(fh.Id))
            : [];
        var feeHeadMap = feeHeads.ToDictionary(fh => fh.Id, fh => fh.Name);

        var requestedByName = request.CreatorId.HasValue
            ? (await identityUserRepository.FindAsync(request.CreatorId.Value))?.Name
            : null;
        var processedByName = request.ProcessedByUserId.HasValue
            ? (await identityUserRepository.FindAsync(request.ProcessedByUserId.Value))?.Name
            : null;

        return new FeeAdjustmentDetailDto
        {
            Id = request.Id,
            VoucherNumber = request.VoucherNumber,
            StudentId = request.StudentId,
            StudentName = FormatStudentName(student),
            StudentGrNo = student.GrNo,
            RollNo = student.RollNo?.ToString(),
            ClassName = standard?.Name,
            DivisionName = division?.Name,
            ContactNo = student.ContactDetail?.PrimaryContactNo,
            AcademicYearId = request.AcademicYearId,
            BoardId = request.BoardId,
            EducationMediumId = request.EducationMediumId,
            AdjustmentType = request.AdjustmentType,
            AdjustmentTypeName = FormatAdjustmentType(request.AdjustmentType),
            Reason = request.Reason,
            Remarks = request.Remarks,
            Status = request.Status,
            StatusName = FormatStatus(request.Status),
            RequestedAmount = request.RequestedAmount,
            AppliedAmount = request.AppliedAmount,
            RemainingAmount = request.RemainingAmount,
            RequestedByUserId = request.CreatorId,
            RequestedByName = requestedByName,
            ProcessedByUserId = request.ProcessedByUserId,
            ProcessedByName = processedByName,
            ProcessedAt = request.ProcessedAt,
            RejectionReason = request.RejectionReason,
            ApproverNotes = request.ApproverNotes,
            CreatedAt = request.CreationTime,
            SourceLines = request.SourceLines.Select(l =>
            {
                dueFeeMap.TryGetValue(l.DueFeeId, out var df);
                return new FeeAdjustmentSourceLineDto
                {
                    DueFeeId = l.DueFeeId,
                    FeeHeadId = l.FeeHeadId,
                    FeeHeadName = feeHeadMap.GetValueOrDefault(l.FeeHeadId, string.Empty),
                    InstallmentLabel = FormatMonthLabel(l.Month, l.Year),
                    Month = l.Month,
                    Year = l.Year,
                    PaidAmount = df?.PaidAmount ?? 0,
                    AdjustableAmount = df?.AdjustableBalance ?? 0,
                    RequestedAmount = l.Amount,
                };
            }).ToList(),
            TargetAllocations = request.TargetAllocations.Select(a => new FeeAdjustmentTargetAllocationDto
            {
                DueFeeId = a.DueFeeId,
                FeeHeadId = a.FeeHeadId,
                FeeHeadName = feeHeadMap.GetValueOrDefault(a.FeeHeadId, string.Empty),
                InstallmentLabel = FormatMonthLabel(a.Month, a.Year),
                Month = a.Month,
                Year = a.Year,
                Amount = a.Amount,
                ApplyBatchNumber = a.ApplyBatchNumber,
                AppliedOn = a.AppliedOn,
            }).ToList(),
        };
    }

    private async Task<ApplyFeeAdjustmentResultDto> MapApplyResultAsync(
        FeeAdjustmentRequest request,
        Guid approverId,
        double appliedInBatch)
    {
        var feeHeadIds = request.TargetAllocations.Select(a => a.FeeHeadId).Distinct().ToList();
        var feeHeads = feeHeadIds.Count > 0
            ? await feeHeadRepository.GetListAsync(fh => feeHeadIds.Contains(fh.Id))
            : [];
        var feeHeadMap = feeHeads.ToDictionary(fh => fh.Id, fh => fh.Name);

        var lastBatch = request.ApplyVersion;
        var batchLines = request.TargetAllocations
            .Where(a => a.ApplyBatchNumber == lastBatch)
            .ToList();

        var dueFeeIds = batchLines.Select(l => l.DueFeeId).ToList();
        var dueFees = dueFeeIds.Count > 0
            ? await dueFeeRepository.GetListAsync(df => dueFeeIds.Contains(df.Id))
            : [];
        var dueFeeMap = dueFees.ToDictionary(df => df.Id);

        var processedByName = (await identityUserRepository.FindAsync(approverId))?.Name;

        return new ApplyFeeAdjustmentResultDto
        {
            AdjustmentId = request.Id,
            Status = request.Status,
            StatusName = FormatStatus(request.Status),
            TotalAppliedAmount = request.AppliedAmount,
            RemainingAmount = request.RemainingAmount,
            AppliedInThisBatch = appliedInBatch,
            SelectedInstallmentsCount = batchLines.Count,
            ProcessedAt = request.ProcessedAt ?? Clock.Now,
            ProcessedByUserId = approverId,
            ProcessedByName = processedByName,
            Lines = batchLines.Select(a =>
            {
                dueFeeMap.TryGetValue(a.DueFeeId, out var df);
                var originalPending = (df?.OutstandingAmount ?? 0) + a.Amount;
                return new ApplyFeeAdjustmentLineResultDto
                {
                    DueFeeId = a.DueFeeId,
                    FeeHeadId = a.FeeHeadId,
                    FeeHeadName = feeHeadMap.GetValueOrDefault(a.FeeHeadId, string.Empty),
                    InstallmentLabel = FormatMonthLabel(a.Month, a.Year),
                    OriginalPendingAmount = originalPending,
                    AppliedAmount = a.Amount,
                    FinalPendingAmount = df?.OutstandingAmount ?? 0,
                };
            }).ToList(),
        };
    }

    private static FinanceAuditStudentContext BuildStudentContext(Student student, string voucher) =>
        new()
        {
            StudentId = student.Id,
            StudentName = FormatStudentName(student),
            GrNo = student.GrNo?.ToString(),
            RollNo = student.RollNo?.ToString(),
        };

    private static string FormatStudentName(Student? student)
    {
        if (student == null) return string.Empty;
        return $"{student.FirstName}{(string.IsNullOrEmpty(student.MiddleName) ? " " : $" {student.MiddleName} ")}{student.LastName}".Trim();
    }

    private static string FormatMonthLabel(MonthEnum month, int year)
    {
        var idx = (int)month;
        var name = idx >= 1 && idx <= 12 ? MonthNames[idx - 1] : month.ToString();
        return $"{name.Substring(0, 3)} {year}";
    }

    private static string FormatStatus(FeeAdjustmentStatus status) => status switch
    {
        FeeAdjustmentStatus.Pending => "Pending",
        FeeAdjustmentStatus.Rejected => "Rejected",
        FeeAdjustmentStatus.PartiallyApplied => "Partially Applied",
        FeeAdjustmentStatus.Applied => "Applied",
        FeeAdjustmentStatus.Cancelled => "Cancelled",
        _ => status.ToString(),
    };

    private static string FormatAdjustmentType(FeeAdjustmentType type) => type switch
    {
        FeeAdjustmentType.FeeCorrection => "Fee Correction",
        FeeAdjustmentType.OverpaymentAdjustment => "Overpayment Adjustment",
        FeeAdjustmentType.DiscountAdjustment => "Discount Adjustment",
        FeeAdjustmentType.RefundAdjustment => "Refund Adjustment",
        FeeAdjustmentType.ManualAdjustment => "Manual Adjustment",
        FeeAdjustmentType.ScholarshipAdjustment => "Scholarship Adjustment",
        _ => type.ToString(),
    };
}

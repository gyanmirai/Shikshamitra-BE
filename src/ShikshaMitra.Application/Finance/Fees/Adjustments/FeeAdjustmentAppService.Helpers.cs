using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Aggregates;
using ShikshaMitra.Students;

namespace ShikshaMitra.Finance.Fees.FeeAdjustment;

public partial class FeeAdjustmentAppService
{
    private async Task<FeeAdjustmentDetailDto> MapDetailAsync(FeeAdjustmentRequest request)
    {
        var student = await studentRepository.GetAsync(request.StudentId);
        var standard = student.StandardId.HasValue ? await standardRepository.FindAsync(student.StandardId.Value) : null;
        var division = student.DivisionId.HasValue ? await divisionRepository.FindAsync(student.DivisionId.Value) : null;

        var dueFeeIds = request.SourceLines.Select(l => l.DueFeeId)
            .Concat(request.TargetAllocations.Select(a => a.DueFeeId)).Distinct().ToList();
        var dueFees = dueFeeIds.Count > 0
            ? await dueFeeRepository.GetListAsync(df => dueFeeIds.Contains(df.Id)) : [];
        var dueFeeMap = dueFees.ToDictionary(df => df.Id);

        var feeHeadIds = dueFees.Select(df => df.FeeHeadId).Distinct().ToList();
        var feeHeads = feeHeadIds.Count > 0
            ? await feeHeadRepository.GetListAsync(fh => feeHeadIds.Contains(fh.Id)) : [];
        var feeHeadMap = feeHeads.ToDictionary(fh => fh.Id, fh => fh.Name);

        var requestedByName = request.CreatorId.HasValue
            ? (await identityUserRepository.FindAsync(request.CreatorId.Value))?.Name : null;
        var processedByName = request.ProcessedByUserId.HasValue
            ? (await identityUserRepository.FindAsync(request.ProcessedByUserId.Value))?.Name : null;

        return new FeeAdjustmentDetailDto
        {
            Id = request.Id, VoucherNumber = request.VoucherNumber, StudentId = request.StudentId,
            StudentName = FormatStudentName(student), StudentGrNo = student.GrNo,
            RollNo = student.RollNo?.ToString(), ClassName = standard?.Name, DivisionName = division?.Name,
            ContactNo = student.ContactDetail?.PrimaryContactNo, AcademicYearId = request.AcademicYearId,
            BoardId = request.BoardId, EducationMediumId = request.EducationMediumId,
            Reason = request.Reason, Status = request.Status,
            StatusName = FormatStatus(request.Status), RequestedAmount = request.RequestedAmount,
            AppliedAmount = request.AppliedAmount, RemainingAmount = request.RemainingAmount,
            RequestedByUserId = request.CreatorId, RequestedByName = requestedByName,
            ProcessedByUserId = request.ProcessedByUserId, ProcessedByName = processedByName,
            ProcessedAt = request.ProcessedAt, RejectionReason = request.RejectionReason,
            ApproverNotes = request.ApproverNotes, CreatedAt = request.CreationTime,
            SourceLines = request.SourceLines.Select(l =>
            {
                dueFeeMap.TryGetValue(l.DueFeeId, out var df);
                return new FeeAdjustmentSourceLineDto
                {
                    DueFeeId = l.DueFeeId, FeeHeadId = l.FeeHeadId,
                    FeeHeadName = feeHeadMap.GetValueOrDefault(l.FeeHeadId, string.Empty),
                    InstallmentLabel = df?.GetDisplayInstallmentLabel() ?? FormatMonthLabel(l.Month, l.Year),
                    Month = l.Month, Year = l.Year,
					PaidAmount = (double)(df?.PaidAmount ?? 0m), AdjustableAmount = (double)(df?.AdjustableBalance ?? 0m),
                    RequestedAmount = l.Amount,
                };
            }).ToList(),
            TargetAllocations = request.TargetAllocations.Select(a =>
            {
                dueFeeMap.TryGetValue(a.DueFeeId, out var targetDue);
                return new FeeAdjustmentTargetAllocationDto
                {
                    DueFeeId = a.DueFeeId, FeeHeadId = a.FeeHeadId,
                    FeeHeadName = feeHeadMap.GetValueOrDefault(a.FeeHeadId, string.Empty),
                    InstallmentLabel = targetDue?.GetDisplayInstallmentLabel() ?? FormatMonthLabel(a.Month, a.Year),
                    Month = a.Month, Year = a.Year,
                    Amount = a.Amount, ApplyBatchNumber = a.ApplyBatchNumber, AppliedOn = a.AppliedOn,
                };
            }).ToList(),
        };
    }

    private async Task<ApplyFeeAdjustmentResultDto> MapApplyResultAsync(
        FeeAdjustmentRequest request, Guid approverId, double appliedInBatch)
    {
        var lastBatch = request.ApplyVersion;
        var batchLines = request.TargetAllocations.Where(a => a.ApplyBatchNumber == lastBatch).ToList();

        var feeHeadIds = request.TargetAllocations.Select(a => a.FeeHeadId).Distinct().ToList();
        var feeHeads = feeHeadIds.Count > 0
            ? await feeHeadRepository.GetListAsync(fh => feeHeadIds.Contains(fh.Id)) : [];
        var feeHeadMap = feeHeads.ToDictionary(fh => fh.Id, fh => fh.Name);

        var dueFeeIds = batchLines.Select(l => l.DueFeeId).ToList();
        var dueFees = dueFeeIds.Count > 0
            ? await dueFeeRepository.GetListAsync(df => dueFeeIds.Contains(df.Id)) : [];
        var dueFeeMap = dueFees.ToDictionary(df => df.Id);

        var processedByName = (await identityUserRepository.FindAsync(approverId))?.Name;

        return new ApplyFeeAdjustmentResultDto
        {
            AdjustmentId = request.Id, Status = request.Status, StatusName = FormatStatus(request.Status),
            TotalAppliedAmount = request.AppliedAmount, RemainingAmount = request.RemainingAmount,
            AppliedInThisBatch = appliedInBatch, SelectedInstallmentsCount = batchLines.Count,
            ProcessedAt = request.ProcessedAt ?? Clock.Now, ProcessedByUserId = approverId,
            ProcessedByName = processedByName,
            Lines = batchLines.Select(a =>
            {
                dueFeeMap.TryGetValue(a.DueFeeId, out var df);
				var originalPending = (double)(df?.OutstandingAmount ?? 0m) + a.Amount;
                return new ApplyFeeAdjustmentLineResultDto
                {
                    DueFeeId = a.DueFeeId, FeeHeadId = a.FeeHeadId,
                    FeeHeadName = feeHeadMap.GetValueOrDefault(a.FeeHeadId, string.Empty),
                    InstallmentLabel = FormatMonthLabel(a.Month, a.Year),
                    OriginalPendingAmount = originalPending, AppliedAmount = a.Amount,
					FinalPendingAmount = (double)(df?.OutstandingAmount ?? 0m),
                };
            }).ToList(),
        };
    }

    private static FinanceAuditStudentContext BuildStudentContext(Student student, string voucher) =>
        new() { StudentId = student.Id, StudentName = FormatStudentName(student), GrNo = student.GrNo?.ToString(), RollNo = student.RollNo?.ToString() };

    private static string FormatStudentName(Student? student)
    {
        if (student == null) return string.Empty;
        return $"{student.FirstName}{(string.IsNullOrEmpty(student.MiddleName) ? " " : $" {student.MiddleName} ")}{student.LastName}".Trim();
    }

    private static string FormatMonthLabel(MonthEnum month, int year)
    {
        var idx = (int)month;
        var name = idx >= 1 && idx <= 12 ? MonthNames[idx] : month.ToString();
        return $"{name[..3]} {year}";
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

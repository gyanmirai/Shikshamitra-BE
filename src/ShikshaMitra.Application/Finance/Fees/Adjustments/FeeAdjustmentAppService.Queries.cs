using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Common;

namespace ShikshaMitra.Finance.Fees.FeeAdjustment;

public partial class FeeAdjustmentAppService
{
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.View)]
    public async Task<AdjustmentEligibilityDto> GetEligibilityAsync(Guid studentId, Guid academicYearId)
    {
        await financeScopeAccessService.EnsureStudentAccessibleAsync(studentId);

        var dueFees = await dueFeeRepository.GetListAsync(
            df => df.StudentId == studentId && df.OutstandingAmount > 0);
        var pendingSource = await feeAdjustmentRepository.GetPendingSourceAmountsByDueFeeAsync(studentId);

        if (dueFees.Count == 0)
            return new AdjustmentEligibilityDto { StudentId = studentId, AcademicYearId = academicYearId };

        var feeHeadIds = dueFees.Select(df => df.FeeHeadId).Distinct().ToList();
        var feeHeads = await feeHeadRepository.GetListAsync(fh => feeHeadIds.Contains(fh.Id));
        var feeHeadMap = feeHeads.ToDictionary(fh => fh.Id, fh => fh.Name);

        var grouped = dueFees
            .GroupBy(df => df.FeeHeadId)
            .Select(g =>
            {
                var months = g
                    .OrderBy(df => df.Year).ThenBy(df => df.Month)
                    .Select(df =>
                    {
                        pendingSource.TryGetValue(df.Id, out var reserved);
                        var adjustable = Math.Max(0, df.AdjustableBalance - reserved);
                        return new AdjustmentEligibilityMonthDto
                        {
                            DueFeeId = df.Id, Month = df.Month, Year = df.Year,
                            Total = df.OriginalAmount, Paid = df.PaidAmount,
                            Refunded = df.RefundedAmount, Adjusted = df.AdjustmentCreditAmount,
                            Adjustable = adjustable,
                        };
                    })
                    .Where(m => m.Adjustable > 0.001).ToList();

                return new AdjustmentEligibilityFeeHeadDto
                {
                    FeeHeadId = g.Key,
                    FeeHeadName = feeHeadMap.GetValueOrDefault(g.Key, g.Key.ToString()),
                    TotalPaid = g.Sum(df => df.PaidAmount),
                    TotalAdjustable = months.Sum(m => m.Adjustable),
                    Months = months,
                };
            })
            .Where(fh => fh.Months.Count > 0).ToList();

        return new AdjustmentEligibilityDto
        {
            StudentId = studentId, AcademicYearId = academicYearId,
            TotalPaid = grouped.Sum(f => f.TotalPaid),
            TotalAdjustable = grouped.Sum(f => f.TotalAdjustable),
            TotalAlreadyAdjusted = dueFees.Sum(df => df.AdjustmentCreditAmount),
            FeeHeads = grouped,
        };
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.View)]
    public async Task<FeeAdjustmentDetailDto> GetAsync(Guid id)
    {
        var request = await feeAdjustmentRepository.GetWithDetailsAsync(id);
        await financeScopeAccessService.EnsureStudentAccessibleAsync(request.StudentId);
        return await MapDetailAsync(request);
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.View)]
    public async Task<FeeAdjustmentApplicationsDto> GetApplicationsAsync(GetFeeAdjustmentApplicationsInput input)
    {
        FinanceClassDivisionFilterValidator.Validate(input);
        await financeScopeAccessService.EnsureAcademicContextAccessibleAsync(
            input.AcademicYearId, input.BoardId, input.EducationMediumId, input.StandardId, input.DivisionId);

        var pageSize = Math.Min(Math.Max(input.PageSize, 1), 100);
        var skip = (Math.Max(input.Page, 1) - 1) * pageSize;

        var counts = await feeAdjustmentRepository.GetStatusCountsAsync(
            input.BoardId, input.EducationMediumId, input.AcademicYearId, input.StandardId, input.DivisionId);
        var items = await feeAdjustmentRepository.GetListForApplicationsAsync(
            input.BoardId, input.EducationMediumId, input.AcademicYearId, input.Status,
            input.CompletedOnly, input.Filter, input.StandardId, input.DivisionId, skip, pageSize);
        var total = await feeAdjustmentRepository.CountForApplicationsAsync(
            input.BoardId, input.EducationMediumId, input.AcademicYearId, input.Status,
            input.CompletedOnly, input.Filter, input.StandardId, input.DivisionId);

        // Batch-load related entities
        var studentIds = items.Select(i => i.StudentId).Distinct().ToList();
        var students = studentIds.Count > 0
            ? await studentRepository.GetListAsync(s => studentIds.Contains(s.Id)) : [];
        var studentMap = students.ToDictionary(s => s.Id);

        var standardIds = students.Where(s => s.StandardId.HasValue).Select(s => s.StandardId!.Value).Distinct().ToList();
        var divisionIds = students.Where(s => s.DivisionId.HasValue).Select(s => s.DivisionId!.Value).Distinct().ToList();
        var standards = standardIds.Count > 0 ? await standardRepository.GetListAsync(s => standardIds.Contains(s.Id)) : [];
        var divisions = divisionIds.Count > 0 ? await divisionRepository.GetListAsync(d => divisionIds.Contains(d.Id)) : [];
        var standardMap = standards.ToDictionary(s => s.Id, s => s.Name);
        var divisionMap = divisions.ToDictionary(d => d.Id, d => d.Name);

        var userIds = items.Where(i => i.CreatorId.HasValue).Select(i => i.CreatorId!.Value).Distinct().ToList();
        var users = userIds.Count > 0 ? await identityUserRepository.GetListAsync(u => userIds.Contains(u.Id)) : [];
        var userMap = users.ToDictionary(u => u.Id, u => u.Name ?? u.UserName ?? string.Empty);

        return new FeeAdjustmentApplicationsDto
        {
            PendingCount = counts.GetValueOrDefault(FeeAdjustmentStatus.Pending),
            PartiallyAppliedCount = counts.GetValueOrDefault(FeeAdjustmentStatus.PartiallyApplied),
            AppliedCount = counts.GetValueOrDefault(FeeAdjustmentStatus.Applied),
            RejectedCount = counts.GetValueOrDefault(FeeAdjustmentStatus.Rejected),
            CancelledCount = counts.GetValueOrDefault(FeeAdjustmentStatus.Cancelled),
            TotalCount = total,
            CurrentPage = input.Page, PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize),
            Items = items.Select(r =>
            {
                studentMap.TryGetValue(r.StudentId, out var student);
                return new FeeAdjustmentApplicationItemDto
                {
                    Id = r.Id, StudentId = r.StudentId,
                    StudentName = FormatStudentName(student),
                    GrNumber = student?.GrNo?.ToString() ?? string.Empty,
                    RollNo = student?.RollNo?.ToString() ?? string.Empty,
                    StandardName = student?.StandardId is Guid sid && standardMap.TryGetValue(sid, out var sn) ? sn : string.Empty,
                    DivisionName = student?.DivisionId is Guid did && divisionMap.TryGetValue(did, out var dn) ? dn : string.Empty,
                    RequestedAmount = r.RequestedAmount, AppliedAmount = r.AppliedAmount,
                    RemainingAmount = r.RemainingAmount, Status = r.Status,
                    StatusName = FormatStatus(r.Status),
                    RequestedByName = r.CreatorId.HasValue && userMap.TryGetValue(r.CreatorId.Value, out var un) ? un : string.Empty,
                    CreatedAt = r.CreationTime,
                };
            }).ToList(),
        };
    }
}

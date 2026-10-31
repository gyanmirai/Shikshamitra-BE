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

        var feeHeads = await feeHeadRepository.GetListAsync(fh => fh.AcademicYearId == academicYearId);
        var feeHeadIds = feeHeads.Select(fh => fh.Id).ToList();
        var dueFees = await dueFeeRepository.GetListAsync(
            df => df.StudentId == studentId && feeHeadIds.Contains(df.FeeHeadId) && df.OutstandingAmount > 0);
        var pendingSource = await feeAdjustmentRepository.GetPendingSourceAmountsByDueFeeAsync(studentId);

        if (dueFees.Count == 0)
            return new AdjustmentEligibilityDto { StudentId = studentId, AcademicYearId = academicYearId };

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
						var adjustable = (double)Math.Max(0m, df.AdjustableBalance - (decimal)reserved);
                        return new AdjustmentEligibilityMonthDto
                        {
                            DueFeeId = df.Id, Month = df.Month, Year = df.Year,
							Total = (double)df.OriginalAmount, Paid = (double)df.PaidAmount,
							Refunded = (double)df.RefundedAmount, Adjusted = (double)df.AdjustmentCreditAmount,
                            Adjustable = adjustable,
                        };
                    })
                    .Where(m => m.Adjustable > 0.001).ToList();

                return new AdjustmentEligibilityFeeHeadDto
                {
                    FeeHeadId = g.Key,
                    FeeHeadName = feeHeadMap.GetValueOrDefault(g.Key, g.Key.ToString()),
					TotalPaid = (double)g.Sum(df => df.PaidAmount),
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
			TotalAlreadyAdjusted = (double)dueFees.Sum(df => df.AdjustmentCreditAmount),
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
        var tableQuery = ParseListFilters(input.Filters, input.Filter);
        var hasCriteria = tableQuery.Filters.Count > 0 || !string.IsNullOrEmpty(tableQuery.Search);
        var page = Math.Max(input.Page, 1);
        var pageSize = Math.Clamp(input.PageSize, 1, 100);
        var skip = (int)Math.Min((long)(page - 1) * pageSize, int.MaxValue);

        // Count and list share exactly the same database-enforced tenant/student/context scope.
        var query = await BuildApplicationsListQueryAsync(input);
        var counts = (await AsyncExecuter.ToListAsync(query.GroupBy(row => row.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })))
            .ToDictionary(row => row.Status, row => row.Count);
        if (input.CompletedOnly == true)
            query = query.Where(row => row.Status == FeeAdjustmentStatus.PartiallyApplied || row.Status == FeeAdjustmentStatus.Applied);
        else if (input.Status.HasValue)
            query = query.Where(row => row.Status == input.Status.Value);
        var total = hasCriteria ? 0 : await AsyncExecuter.CountAsync(query);
        var ordered = query.OrderByDescending(row => row.CreationTime);
        // Only criteria requests need complete DTO display values; ordinary requests remain provider-paged.
        var items = await AsyncExecuter.ToListAsync(hasCriteria ? ordered : ordered.Skip(skip).Take(pageSize));

        var studentIds = items.Select(item => item.StudentId).Distinct().ToList();
        // Project scalars: materializing Student roots would load the configured profile/academic AutoInclude graph.
        var students = studentIds.Count > 0
            ? await AsyncExecuter.ToListAsync((await studentRepository.GetQueryableAsync())
                .Where(student => studentIds.Contains(student.Id) && student.TenantId == CurrentTenant.Id)
                .Select(student => new
                {
                    student.Id, student.FirstName, student.MiddleName, student.LastName,
                    student.GrNo, student.RollNo, student.StandardId, student.DivisionId
                })) : [];
        var studentMap = students.ToDictionary(student => student.Id);
        var standardIds = students.Where(student => student.StandardId.HasValue).Select(student => student.StandardId!.Value).Distinct().ToList();
        var divisionIds = students.Where(student => student.DivisionId.HasValue).Select(student => student.DivisionId!.Value).Distinct().ToList();
        var standards = standardIds.Count > 0
            ? await AsyncExecuter.ToListAsync((await standardRepository.GetQueryableAsync())
                .Where(standard => standardIds.Contains(standard.Id)).Select(standard => new { standard.Id, standard.Name })) : [];
        var divisions = divisionIds.Count > 0
            ? await AsyncExecuter.ToListAsync((await divisionRepository.GetQueryableAsync())
                .Where(division => divisionIds.Contains(division.Id)).Select(division => new { division.Id, division.Name })) : [];
        var standardMap = standards.ToDictionary(standard => standard.Id, standard => standard.Name);
        var divisionMap = divisions.ToDictionary(division => division.Id, division => division.Name);
        var userIds = items.Where(item => item.CreatorId.HasValue).Select(item => item.CreatorId!.Value).Distinct().ToList();
        var users = userIds.Count > 0
            ? await AsyncExecuter.ToListAsync((await identityUserRepository.GetQueryableAsync())
                .Where(user => userIds.Contains(user.Id)).Select(user => new { user.Id, user.Name, user.UserName })) : [];
        var userMap = users.ToDictionary(user => user.Id, user => user.Name ?? user.UserName ?? string.Empty);
        var dtos = items.Select(request =>
        {
            studentMap.TryGetValue(request.StudentId, out var student);
            return new FeeAdjustmentApplicationItemDto
            {
                Id = request.Id, StudentId = request.StudentId,
                StudentName = student == null ? string.Empty
                    : $"{student.FirstName}{(string.IsNullOrEmpty(student.MiddleName) ? " " : $" {student.MiddleName} ")}{student.LastName}".Trim(),
                GrNumber = student?.GrNo?.ToString() ?? string.Empty,
                RollNo = student?.RollNo?.ToString() ?? string.Empty,
                StandardName = student?.StandardId is Guid sid && standardMap.TryGetValue(sid, out var sn) ? sn : string.Empty,
                DivisionName = student?.DivisionId is Guid did && divisionMap.TryGetValue(did, out var dn) ? dn : string.Empty,
                RequestedAmount = request.RequestedAmount, AppliedAmount = request.AppliedAmount,
                RemainingAmount = request.RemainingAmount, Status = request.Status,
                StatusName = FormatStatus(request.Status),
                RequestedByName = request.CreatorId.HasValue && userMap.TryGetValue(request.CreatorId.Value, out var un) ? un : string.Empty,
                CreatedAt = request.CreationTime,
            };
        }).ToList();
        if (hasCriteria)
        {
            if (!string.IsNullOrEmpty(tableQuery.Search))
            {
                var requests = items.ToDictionary(request => request.Id);
                dtos = dtos.Where(row => MatchesListSearch(row, requests[row.Id], tableQuery.Search)).ToList();
            }
            dtos = tableQuery.Apply(dtos.AsQueryable(), ListFilterFields).ToList();
            total = dtos.Count;
            dtos = dtos.Skip(skip).Take(pageSize).ToList();
        }
        return new FeeAdjustmentApplicationsDto
        {
            PendingCount = counts.GetValueOrDefault(FeeAdjustmentStatus.Pending),
            PartiallyAppliedCount = counts.GetValueOrDefault(FeeAdjustmentStatus.PartiallyApplied),
            AppliedCount = counts.GetValueOrDefault(FeeAdjustmentStatus.Applied),
            RejectedCount = counts.GetValueOrDefault(FeeAdjustmentStatus.Rejected),
            CancelledCount = counts.GetValueOrDefault(FeeAdjustmentStatus.Cancelled),
            TotalCount = total, CurrentPage = page, PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize), Items = dtos,
        };
    }
}

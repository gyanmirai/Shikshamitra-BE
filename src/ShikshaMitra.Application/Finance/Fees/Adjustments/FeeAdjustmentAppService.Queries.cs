using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.Collections;

namespace ShikshaMitra.Finance.Fees.FeeAdjustment;

public partial class FeeAdjustmentAppService
{
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.View)]
    public async Task<AdjustmentEligibilityDto> GetEligibilityAsync(Guid studentId, Guid academicYearId)
    {
        await financeScopeAccessService.EnsureRequestStudentAccessibleAsync(studentId, academicYearId,
            ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.View);
        var feeHeads = await feeHeadRepository.GetListAsync(head => head.AcademicYearId == academicYearId);
        var headIds = feeHeads.Select(head => head.Id).ToList();
        var dues = await dueFeeRepository.GetListAsync(due =>
            due.StudentId == studentId && headIds.Contains(due.FeeHeadId));
        var reservations = await feeRefundRepository.GetActiveReservedAmountsAsync(
            dues.Select(due => due.Id).ToList());
        var settledPaid = await feeAdjustmentManager.GetSettledPaidAmountsAsync(
            dues.Select(due => due.Id).ToList(), studentId, academicYearId);
        var headNames = feeHeads.ToDictionary(head => head.Id, head => head.Name);
        var grouped = dues.GroupBy(due => due.FeeHeadId).Select(group =>
        {
            var months = group.OrderBy(due => due.Year).ThenBy(due => due.Month)
                .Select(due =>
                {
                    var unpaid = due.OutstandingAmount > 0m;
                    var settledCredit = Math.Max(0m,
                        settledPaid.GetValueOrDefault(due.Id) - due.RefundedAmount +
                        due.NetAmount - due.EffectiveObligationAmount -
                        due.ReservedPaidCreditAmount - due.TransferredPaidCreditAmount);
                    return new AdjustmentEligibilityMonthDto
                    {
                        DueFeeId = due.Id, Month = due.Month, Year = due.Year,
                        Kind = unpaid ? FeeAdjustmentKind.UnpaidTransfer : FeeAdjustmentKind.PaidCredit,
                        Status = DueFeeCollectionStatusResolver.Resolve(due, Clock.Now, 0d),
                        OutstandingAmount = (double)due.OutstandingAmount,
                        Total = (double)due.EffectiveObligationAmount, Paid = (double)due.PaidAmount,
                        Refunded = (double)due.RefundedAmount, Adjusted = (double)due.AdjustmentCreditAmount,
                        Adjustable = unpaid ? (double)due.OutstandingAmount : (double)Math.Max(0m,
                            Math.Min(due.AdjustableBalance, settledCredit) -
                            reservations.GetValueOrDefault(due.Id)),
                    };
                }).Where(month => month.Adjustable > 0).ToList();
            return new AdjustmentEligibilityFeeHeadDto
            {
                FeeHeadId = group.Key,
                FeeHeadName = headNames.GetValueOrDefault(group.Key, group.Key.ToString()),
                TotalPaid = (double)group.Sum(due => due.PaidAmount),
                TotalAdjustable = months.Sum(month => month.Adjustable),
                Months = months,
            };
        }).Where(head => head.Months.Count > 0).ToList();
        return new AdjustmentEligibilityDto
        {
            StudentId = studentId, AcademicYearId = academicYearId,
            TotalPaid = (double)dues.Sum(due => due.PaidAmount),
            TotalAdjustable = grouped.Sum(head => head.TotalAdjustable),
            TotalAlreadyAdjusted = (double)dues.Sum(due => due.AdjustmentCreditAmount),
            FeeHeads = grouped,
        };
    }

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.View)]
    public async Task<FeeAdjustmentDetailDto> GetAsync(Guid id)
    {
        var request = await feeAdjustmentRepository.GetWithDetailsAsync(id);
        await financeScopeAccessService.EnsureRequestStudentAccessibleAsync(request.StudentId, request.AcademicYearId,
            ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.View);
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
                UnavailableReason = request.HasPaidCreditReservation || request.Kind == FeeAdjustmentKind.UnpaidTransfer
                    ? null : PaidCreditTransferUnavailableReason,
                RequestedByUserId = request.CreatorId,
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
        var visibleRequests = items.ToDictionary(request => request.Id);
        foreach (var row in dtos)
            row.AvailableActions = await GetAvailableActionsAsync(visibleRequests[row.Id]);
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

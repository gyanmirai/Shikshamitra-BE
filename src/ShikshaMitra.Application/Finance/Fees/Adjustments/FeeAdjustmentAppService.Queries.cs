using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Aggregates;

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
        var students = await studentRepository.GetQueryableAsync();
        var standards = await standardRepository.GetQueryableAsync();
        var divisions = await divisionRepository.GetQueryableAsync();
        var users = await identityUserRepository.GetQueryableAsync();
        // Project only list scalars. This prevents the request and student AutoInclude
        // graphs from being loaded before filtering and paging.
        var rows = from request in query
            join student in students on request.StudentId equals student.Id
            join standard in standards on request.StandardId equals standard.Id into standardJoin
            from standard in standardJoin.DefaultIfEmpty()
            join division in divisions on request.DivisionId equals division.Id into divisionJoin
            from division in divisionJoin.DefaultIfEmpty()
            join user in users on request.CreatorId equals (Guid?)user.Id into userJoin
            from user in userJoin.DefaultIfEmpty()
            select new AdjustmentListRow
            {
                Id = request.Id, StudentId = request.StudentId, CreatorId = request.CreatorId,
                StudentName = student.FirstName + (student.MiddleName == null || student.MiddleName == "" ? " " : " " + student.MiddleName + " ") + student.LastName,
                GrNumber = student.GrNo.HasValue ? student.GrNo.Value.ToString() : "",
                RollNo = student.RollNo.HasValue ? student.RollNo.Value.ToString() : "",
                StandardName = standard == null ? "" : standard.Name,
                DivisionName = division == null ? "" : division.Name,
                RequestedByName = user == null ? "" : (user.Name ?? user.UserName ?? ""),
                StatusName = request.Status == FeeAdjustmentStatus.Pending ? "Pending" :
                    request.Status == FeeAdjustmentStatus.Rejected ? "Rejected" :
                    request.Status == FeeAdjustmentStatus.PartiallyApplied ? "Partially applied" :
                    request.Status == FeeAdjustmentStatus.Applied ? "Approved" : "Cancelled",
                Reason = request.Reason, VoucherNumber = request.VoucherNumber,
                RequestedAmount = request.RequestedAmount, AppliedAmount = request.AppliedAmount,
                RemainingAmount = request.RemainingAmount, Status = request.Status,
                CreatedAt = request.CreationTime
            };
        rows = tableQuery.Apply(rows, ListFilterFields);
        if (!string.IsNullOrEmpty(tableQuery.Search))
        {
            var search = tableQuery.Search.ToLowerInvariant();
            var isDate = DateTime.TryParseExact(tableQuery.Search,
                ["dd-MM-yyyy", "yyyy-MM-dd", "O"], CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var parsedDate);
            var nextDate = parsedDate.Date.AddDays(1);
            var isAmount = double.TryParse(tableQuery.Search, NumberStyles.Number,
                CultureInfo.InvariantCulture, out var parsedAmount);
            rows = rows.Where(row =>
                row.StudentName.ToLower().Contains(search) || row.GrNumber.Contains(search) ||
                (row.StudentName + " (" + row.GrNumber + ")").ToLower().Contains(search) ||
                row.RollNo.Contains(search) || row.StandardName.ToLower().Contains(search) ||
                (row.StandardName + " / " + row.DivisionName).ToLower().Contains(search) ||
                row.DivisionName.ToLower().Contains(search) || row.RequestedByName.ToLower().Contains(search) ||
                row.StatusName.ToLower().Contains(search) || row.VoucherNumber.ToLower().Contains(search) ||
                row.Reason.ToLower().Contains(search) ||
                (isAmount && row.RequestedAmount == parsedAmount) ||
                (isDate && row.CreatedAt >= parsedDate.Date && row.CreatedAt < nextDate));
        }
        var hasCriteria = tableQuery.Filters.Count > 0 || !string.IsNullOrEmpty(tableQuery.Search);
        var total = hasCriteria ? await AsyncExecuter.CountAsync(rows) : await AsyncExecuter.CountAsync(query);
        var pageRows = await AsyncExecuter.ToListAsync(rows.OrderByDescending(row => row.CreatedAt)
            .ThenBy(row => row.Id).Skip(skip).Take(pageSize));
        var pageIds = pageRows.Select(row => row.Id).ToList();
        var visibleRequests = (await feeAdjustmentRepository.GetListForActionAvailabilityAsync(pageIds))
            .ToDictionary(request => request.Id);
        var mayApprove = await AuthorizationService.IsGrantedAsync(
            ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Approve);
        var mayReject = await AuthorizationService.IsGrantedAsync(
            ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Reject);
        var isAdministrator = (mayApprove || mayReject) && currentUser.Id.HasValue &&
            await userScopeService.IsAdminUserAsync(currentUser.Id.Value);
        var dtos = new List<FeeAdjustmentApplicationItemDto>(pageRows.Count);
        foreach (var row in pageRows)
        {
            var request = visibleRequests[row.Id];
            dtos.Add(new FeeAdjustmentApplicationItemDto
            {
                Id = row.Id, StudentId = row.StudentId, RequestedByUserId = row.CreatorId,
                StudentName = row.StudentName, GrNumber = row.GrNumber, RollNo = row.RollNo,
                StandardName = row.StandardName, DivisionName = row.DivisionName,
                RequestedByName = row.RequestedByName, StatusName = row.StatusName,
                RequestedAmount = row.RequestedAmount, AppliedAmount = row.AppliedAmount,
                RemainingAmount = row.RemainingAmount, Status = row.Status, CreatedAt = row.CreatedAt,
                UnavailableReason = request.HasPaidCreditReservation || request.Kind == FeeAdjustmentKind.UnpaidTransfer
                    ? null : PaidCreditTransferUnavailableReason,
                AvailableActions = await GetAvailableActionsAsync(request, mayApprove, mayReject, isAdministrator)
            });
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

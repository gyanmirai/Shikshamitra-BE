using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Accounts;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.FeeRefund.Repositories;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Validation;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Finance;

namespace ShikshaMitra.Finance.Dashboard;

[RemoteService(false)]
[Authorize(ShikshaMitraSubModulePermissions.FinanceDashboard)]
public class FinanceDashboardAppService(
    FinanceScopeAccessService financeScope,
    FeeCollectionSummaryQueryService balances,
    FinanceDashboardActivityReader activity,
    ConcessionRequestAppService concessions,
    FeeRefundAppService refunds,
    IConcessionRequestRepository concessionRepository,
    IFeeRefundRepository refundRepository,
    IAcademicYearRepository years,
    IUserScopeAccessService userScope) : ApplicationService, IFinanceDashboardAppService
{
    public async Task<FinanceDashboardOverviewDto> GetOverviewAsync(Guid academicYearId, DateOnly? from = null,
        DateOnly? to = null, CancellationToken cancellationToken = default)
    {
        await EnsureYearAsync(academicYearId);
        if (from.HasValue != to.HasValue)
            throw InvalidPeriod();

        (DateTime StartUtc, DateTime EndUtc)? period = null;
        if (from.HasValue && to.HasValue)
        {
            await ValidatePeriodAsync(academicYearId, from.Value, to.Value, cancellationToken);
            period = AccountsRegisterPeriod.Resolve(from.Value.ToDateTime(TimeOnly.MinValue),
                to.Value.ToDateTime(TimeOnly.MinValue), null, null);
        }
        var visibleBalances = await CanReadAsync(Permissions.Fees_FeeCollections.View)
            ? await balances.ReadDashboardBalancesAsync(academicYearId,
                await CanReadAsync(Permissions.Fees_Charges.View), cancellationToken, from, to)
            : null;

        var work = new List<FinanceDashboardWorkDto>();
        if (await CanReadAsync(Permissions.Fees_Concession.View))
        {
            var visibleIds = await concessions.GetVisibleConcessionRequestIdsAsync(academicYearId);
            var rows = (await concessionRepository.GetQueryableAsync()).Where(request =>
                request.TenantId == CurrentTenant.Id && request.AcademicYearId == academicYearId &&
                request.Status == ConcessionRequestStatus.Pending && visibleIds.Contains(request.Id));
            if (period is { } concessionPeriod)
                rows = rows.Where(request => (request.SubmittedOn ?? request.CreationTime) >= concessionPeriod.StartUtc &&
                    (request.SubmittedOn ?? request.CreationTime) < concessionPeriod.EndUtc);
            var count = await AsyncExecuter.CountAsync(rows, cancellationToken);
            work.Add(new("concessionReview", count));
        }

        FinanceDashboardWorkDto? awaitingPayment = null;
        if (await CanReadAsync(Permissions.Fees_Refund.View))
        {
            var visibleIds = await refunds.GetVisibleRefundRequestIdsAsync(academicYearId);
            var query = (await refundRepository.GetQueryableAsync())
                .Where(request => request.TenantId == CurrentTenant.Id && request.AcademicYearId == academicYearId &&
                    visibleIds.Contains(request.Id) &&
                    (request.Status == FeeRefundStatus.Submitted || request.Status == FeeRefundStatus.UnderReview ||
                     request.Status == FeeRefundStatus.Approved || request.Status == FeeRefundStatus.Failed));
            var requests = await AsyncExecuter.ToListAsync(query.Select(request => new
                {
                    request.Status, request.SubmittedAt, request.CreationTime,
                    Amount = request.ApprovedAmount - request.ProcessedAmount
                }),
                cancellationToken);
            var selected = period is { } refundPeriod
                ? requests.Where(request =>
                    (request.SubmittedAt?.UtcDateTime ?? request.CreationTime) >= refundPeriod.StartUtc &&
                    (request.SubmittedAt?.UtcDateTime ?? request.CreationTime) < refundPeriod.EndUtc)
                : requests;
            var rows = selected.GroupBy(request => request.Status).Select(group => new
                { Status = group.Key, Count = group.Count(), Amount = group.Sum(request => request.Amount) }).ToList();
            var approved = rows.SingleOrDefault(row => row.Status == FeeRefundStatus.Approved);
            var failed = rows.SingleOrDefault(row => row.Status == FeeRefundStatus.Failed);
            awaitingPayment = new("refundPayment", approved?.Count ?? 0, approved?.Amount ?? 0m);
            work.Add(new("refundReview", rows.Where(row =>
                row.Status is FeeRefundStatus.Submitted or FeeRefundStatus.UnderReview).Sum(row => row.Count)));
            work.Add(new("refundFailed", failed?.Count ?? 0, failed?.Amount ?? 0m));
            work.Add(awaitingPayment);
        }

        return new(visibleBalances, work, awaitingPayment);
    }

    public async Task<FinanceDashboardActivityDto> GetActivityAsync(Guid academicYearId, DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default)
    {
        await EnsureYearAsync(academicYearId);
        await ValidatePeriodAsync(academicYearId, from, to, cancellationToken);
        var scope = await userScope.GetCurrentUserScopeAsync();
        return await activity.ReadAsync(academicYearId, from, to,
            await CanReadAsync(Permissions.Fees_FeeCollections.View),
            await CanReadAsync(Permissions.Accounts_IncomeTracking.View),
            await CanReadAsync(Permissions.Accounts_ExpenseTracking.View),
            scope.HasAcademicYearWideScope(academicYearId), cancellationToken);
    }

    private Task<bool> CanReadAsync(string permission) => AuthorizationService.IsGrantedAsync(permission);

    private async Task ValidatePeriodAsync(Guid academicYearId, DateOnly from, DateOnly to,
        CancellationToken cancellationToken)
    {
        var year = await years.GetAsync(academicYearId, cancellationToken: cancellationToken);
        if (from > to || from < DateOnly.FromDateTime(year.StartDate) || to > DateOnly.FromDateTime(year.EndDate))
            throw InvalidPeriod();
    }

    private static AbpValidationException InvalidPeriod() =>
        new("Select dates within the academic year.",
            [new ValidationResult("Select dates within the academic year.", ["from", "to"])]);

    private Task EnsureYearAsync(Guid academicYearId)
    {
        if (!CurrentTenant.Id.HasValue || !CurrentUser.Id.HasValue || academicYearId == Guid.Empty)
            throw new AbpAuthorizationException();

        return financeScope.EnsureCollectionYearAccessibleAsync(academicYearId);
    }
}

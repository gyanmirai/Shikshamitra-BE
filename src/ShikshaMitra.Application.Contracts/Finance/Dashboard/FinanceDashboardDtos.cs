using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Collections;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Dashboard;

public interface IFinanceDashboardAppService : IApplicationService
{
    Task<FinanceDashboardOverviewDto> GetOverviewAsync(Guid academicYearId, DateOnly? from = null,
        DateOnly? to = null, CancellationToken cancellationToken = default);
    Task<FinanceDashboardActivityDto> GetActivityAsync(Guid academicYearId, DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default);
}

public record FinanceDashboardOverviewDto(FinanceDashboardBalancesDto? Balances,
    IReadOnlyList<FinanceDashboardWorkDto> Work, FinanceDashboardWorkDto? RefundsAwaitingPayment);

public record FinanceDashboardBalancesDto(decimal Outstanding, decimal Overdue, int StudentsWithOverdueFees,
    IReadOnlyList<FinanceDashboardClassDto> Classes);

public record FinanceDashboardClassDto(Guid? ClassId, string ClassName, Guid? BoardId, Guid? MediumId,
    string? BoardMedium, decimal Outstanding, decimal Overdue, int StudentsWithOverdueFees, decimal? SeparateCharges);

public record FinanceDashboardWorkDto(string Kind, int Count, decimal? Amount = null);

public record FinanceDashboardActivityDto(decimal? ConfirmedFees, int? ReceiptCount,
    IReadOnlyList<FinanceDashboardDayDto> Days, decimal? ConfirmedIncome, decimal? ConfirmedExpenses,
    int? AccountEntryCount, string? CollectionsUnavailable,
    IReadOnlyList<FinanceDashboardPaymentMethodDto> PaymentMethods)
{
    public decimal? NetMovement => ConfirmedIncome - ConfirmedExpenses;
}

public record FinanceDashboardDayDto(DateOnly Date, decimal Amount);

public record FinanceDashboardPaymentMethodDto(PaymentMethodEnum Method, decimal Amount);

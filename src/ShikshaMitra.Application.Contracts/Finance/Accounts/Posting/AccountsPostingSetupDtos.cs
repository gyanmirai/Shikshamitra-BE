using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Collections;

namespace ShikshaMitra.Finance.Accounts.Posting;

public record AccountingPeriodDto(Guid Id, string Code, DateOnly StartsOn, DateOnly EndsOn, bool IsOpen);
public record PaymentDestinationDto(Guid Id, PaymentMethodEnum Method, PaymentDirection Direction,
    Guid AssetAccountHeadId, Guid? BankAccountId, DateOnly EffectiveFrom, DateOnly? EffectiveTo, bool IsActive);
public record AccountsPostingSetupDto(Guid AcademicYearId, bool IsEnabled, DateOnly? FirstPostingDate,
    Guid? CashAccountHeadId, IReadOnlyList<AccountingPeriodDto> Periods,
    IReadOnlyList<PaymentDestinationDto> Destinations, IReadOnlyList<string> ReadinessIssues);
public record ConfigureAccountsPostingDto(DateOnly FirstPostingDate, Guid CashAccountHeadId);
public record CreateAccountingPeriodDto(string Code, DateOnly StartsOn, DateOnly EndsOn);
public record CreatePaymentDestinationDto(PaymentMethodEnum Method, PaymentDirection Direction,
    Guid AssetAccountHeadId, Guid? BankAccountId, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
public record EndPaymentDestinationDto(DateOnly LastEffectiveDate);

public interface IAccountsPostingSetupAppService
{
    Task<AccountsPostingSetupDto> GetAsync(Guid academicYearId);
    Task<AccountsPostingSetupDto> ConfigureAsync(Guid academicYearId, ConfigureAccountsPostingDto input);
    Task<AccountsPostingSetupDto> AddPeriodAsync(Guid academicYearId, CreateAccountingPeriodDto input);
    Task<AccountsPostingSetupDto> AddDestinationAsync(Guid academicYearId, CreatePaymentDestinationDto input);
    Task<AccountsPostingSetupDto> EndDestinationAsync(Guid academicYearId, Guid id, EndPaymentDestinationDto input);
}

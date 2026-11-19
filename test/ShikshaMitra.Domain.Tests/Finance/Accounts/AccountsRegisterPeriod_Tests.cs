using System;
using ShikshaMitra.Finance.Accounts.Incomes;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;
public class AccountsRegisterPeriod_Tests
{
    [Fact]
    public void Month_boundaries_are_India_calendar_instants_clipped_to_academic_dates()
    {
        var result = AccountsRegisterPeriod.Resolve(new(2026, 6, 15), new(2027, 5, 10), 6, 2026);
        result.StartUtc.ShouldBe(new DateTime(2026, 6, 14, 18, 30, 0, DateTimeKind.Utc));
        result.EndUtc.ShouldBe(new DateTime(2026, 6, 30, 18, 30, 0, DateTimeKind.Utc));
        var last = AccountsRegisterPeriod.Resolve(new(2026, 6, 15), new(2027, 5, 10), 5, 2027);
        last.EndUtc.ShouldBe(new DateTime(2027, 5, 10, 18, 30, 0, DateTimeKind.Utc));
    }
    [Theory]
    [InlineData(5, 2026)] [InlineData(6, 2027)] [InlineData(13, 2026)]
    [InlineData(6, null)] [InlineData(null, 2026)]
    public void Invalid_or_outside_academic_months_are_rejected(int? month, int? year) =>
        Should.Throw<BusinessException>(() => AccountsRegisterPeriod.Resolve(new(2026, 6, 15), new(2027, 5, 10), month, year));
    [Fact]
    public void All_months_cover_the_complete_academic_date_interval()
    {
        var range = AccountsRegisterPeriod.Resolve(new(2026, 6, 15), new(2027, 5, 10), null, null);
        range.StartUtc.Kind.ShouldBe(DateTimeKind.Utc);
        range.EndUtc.ShouldBe(new DateTime(2027, 5, 10, 18, 30, 0, DateTimeKind.Utc));
    }
    [Fact]
    public void Manual_routing_preserves_money_and_rejects_bank_on_cash_and_missing_non_cash_bank()
    {
        var cash = new Transaction(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), "Donation", null, 12.34m, 0, DateTime.UtcNow);
        Should.Throw<BusinessException>(() => cash.SetManualRouting("Donation", Guid.NewGuid(), "Bank", null));
        cash.SetManualRouting("Donation", null, null, " REF ");
        cash.Amount.ShouldBe(12.34m);
        cash.ExtraProperties["PaymentReference"].ShouldBe("REF");
        var bank = new Transaction(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), "Donation", null, 12.34m, 3, DateTime.UtcNow);
        Should.Throw<BusinessException>(() => bank.SetManualRouting("Donation", null, null, null));
        bank.SetManualRouting("Donation", Guid.NewGuid(), "School bank • 1234", "Reference");
        bank.BankLabelSnapshot.ShouldBe("School bank • 1234");
    }
}

using System;
using ShikshaMitra.Finance.Accounts.Posting;
using ShikshaMitra.Finance.Fees.Collections;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

public class AccountingPostingSetup_Tests
{
    [Fact]
    public void Period_has_inclusive_dates_and_can_be_closed()
    {
        var period = new AccountingPeriod(Guid.NewGuid(), Guid.NewGuid(), "FY26",
            new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31));
        period.Contains(new DateOnly(2026, 4, 1)).ShouldBeTrue();
        period.Contains(new DateOnly(2027, 3, 31)).ShouldBeTrue();
        period.Contains(new DateOnly(2027, 4, 1)).ShouldBeFalse();
        period.Close();
        period.IsOpen.ShouldBeFalse();
        Should.Throw<ArgumentException>(() => new AccountingPeriod(Guid.NewGuid(), null,
            "bad", new DateOnly(2027, 4, 1), new DateOnly(2026, 3, 31)));
    }

    [Fact]
    public void Changing_posting_setup_disables_live_posting()
    {
        var setup = new AccountsPostingConfiguration(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        setup.IsEnabled.ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => setup.Enable());
        setup.Configure(new DateOnly(2026, 10, 1), Guid.NewGuid());
        setup.Enable();
        setup.IsEnabled.ShouldBeTrue();
        setup.Configure(new DateOnly(2026, 10, 2), Guid.NewGuid());
        setup.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void Destination_is_effective_only_in_its_active_range()
    {
        var rule = new PaymentDestinationRule(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            PaymentMethodEnum.UPI, PaymentDirection.Incoming, Guid.NewGuid(), null,
            new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));
        rule.AppliesOn(new DateOnly(2026, 10, 1)).ShouldBeTrue();
        rule.AppliesOn(new DateOnly(2026, 10, 31)).ShouldBeTrue();
        rule.AppliesOn(new DateOnly(2026, 11, 1)).ShouldBeFalse();
        rule.EndOn(new DateOnly(2026, 10, 15));
        rule.AppliesOn(new DateOnly(2026, 10, 15)).ShouldBeTrue();
        rule.AppliesOn(new DateOnly(2026, 10, 16)).ShouldBeFalse();
    }
}

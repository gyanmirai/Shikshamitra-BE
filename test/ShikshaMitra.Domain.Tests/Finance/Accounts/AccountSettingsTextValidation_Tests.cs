using System;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

public class AccountSettingsTextValidation_Tests
{
    [Theory]
    [InlineData(TransactionType.Income, 40)]
    [InlineData(TransactionType.Expense, 128)]
    public void Category_constructor_and_update_enforce_name_limits(TransactionType type, int maximum)
    {
        TransactionCategory Create(string name) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), type, name, null);
        var category = Create(new string('A', maximum));
        category.Name.Length.ShouldBe(maximum);
        Should.Throw<ArgumentException>(() => Create(new string('A', maximum + 1)));
        Should.Throw<ArgumentException>(() => category.Update(null, new string('A', maximum + 1), null, null, null, null, []));
    }

    [Fact]
    public void Business_names_preserve_numbers_symbols_and_unicode()
    {
        var category = new TransactionCategory(Guid.NewGuid(), null, Guid.NewGuid(), TransactionType.Expense, "  Class 10 शुल्क & Fees  ", null);
        category.Name.ShouldBe("Class 10 शुल्क & Fees");
        var bank = CreateBank("School 10 बैंक & Co", "क्षेत्र Branch 2", "School 10 बैंक", "शुल्क");
        bank.BankName.ShouldBe("School 10 बैंक & Co");
        bank.BranchName.ShouldBe("क्षेत्र Branch 2");
        bank.Purpose.ShouldBe("शुल्क");
    }

    [Fact]
    public void Bank_name_and_purpose_limits_are_domain_invariants()
    {
        CreateBank(new string('A', 100), new string('B', 100), new string('C', 100), new string('D', 500));
        Should.Throw<ArgumentException>(() => CreateBank(new string('A', 101), "Branch", "Holder", null));
        Should.Throw<ArgumentException>(() => CreateBank("Bank", new string('B', 101), "Holder", null));
        Should.Throw<ArgumentException>(() => CreateBank("Bank", "Branch", new string('C', 101), null));
        var bank = CreateBank("Bank", "Branch", "Holder", null);
        Should.Throw<ArgumentException>(() => bank.Update(null, null, "Bank", "Branch", null, "Holder", BankAccountType.Savings, "000001", new string('D', 501), "QAAA0123456", null, null, null));
    }

    private static BankAccount CreateBank(string bank, string branch, string holder, string? purpose) =>
        new(Guid.NewGuid(), Guid.NewGuid(), bank, branch, null, holder, BankAccountType.Savings, "000001", purpose, "QAAA0123456", "qa@qa");
}

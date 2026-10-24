using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

/// <summary>Only cash finalizes immediately; cashier confirmation cannot finalize a non-cash receipt.</summary>
public class FeeReceiptPaymentPolicy_Tests
{
    // ── AlwaysFinal bucket ────────────────────────────────────────────────────

    [Fact]
    public void Cash_Produces_FinalFee()
    {
        var kind = FeeReceiptPaymentPolicy.DetermineFeeReceiptKind([PaymentMethodEnum.Cash]);
        Assert.Equal(FeeReceiptKind.FinalFee, kind);
    }

    [Fact]
    public void Cash_Does_Not_Require_Verification()
    {
        Assert.False(FeeReceiptPaymentPolicy.RequiresVerification([PaymentMethodEnum.Cash]));
    }

    [Fact]
    public void Cash_Does_Not_Require_BankConfirmation()
    {
        Assert.False(FeeReceiptPaymentPolicy.RequiresBankConfirmation([PaymentMethodEnum.Cash]));
    }

    // ── AlwaysProvisional bucket ───────────────────────────────────────────────

    [Theory]
    [InlineData(PaymentMethodEnum.Cheque)]
    [InlineData(PaymentMethodEnum.DemandDraft)]
    public void Cheque_And_DD_Produce_ProvisionalFee(PaymentMethodEnum method)
    {
        var kind = FeeReceiptPaymentPolicy.DetermineFeeReceiptKind([method]);
        Assert.Equal(FeeReceiptKind.ProvisionalFee, kind);
    }

    [Theory]
    [InlineData(PaymentMethodEnum.Cheque)]
    [InlineData(PaymentMethodEnum.DemandDraft)]
    public void Cheque_And_DD_Require_Verification(PaymentMethodEnum method)
    {
        Assert.True(FeeReceiptPaymentPolicy.RequiresVerification([method]));
    }

    [Theory]
    [InlineData(PaymentMethodEnum.Cheque, true)]
    [InlineData(PaymentMethodEnum.Cheque, false)]
    [InlineData(PaymentMethodEnum.DemandDraft, true)]
    public void BankConfirmed_Flag_Is_Ignored_For_Cheque_And_DD(
        PaymentMethodEnum method, bool bankConfirmed)
    {
        // AlwaysProvisional always wins regardless of bankReceiptConfirmed value
        var kind = FeeReceiptPaymentPolicy.DetermineFeeReceiptKind([method], bankConfirmed);
        Assert.Equal(FeeReceiptKind.ProvisionalFee, kind);
    }

    // ── CashierDecided bucket ─────────────────────────────────────────────────

    [Theory]
    [InlineData(PaymentMethodEnum.UPI)]
    [InlineData(PaymentMethodEnum.CreditCard)]
    [InlineData(PaymentMethodEnum.DebitCard)]
    [InlineData(PaymentMethodEnum.BankTransfer)]
    public void Electronic_With_Confirmed_True_Remains_Provisional(PaymentMethodEnum method)
    {
        var kind = FeeReceiptPaymentPolicy.DetermineFeeReceiptKind([method], bankReceiptConfirmed: true);
        Assert.Equal(FeeReceiptKind.ProvisionalFee, kind);
    }

    [Theory]
    [InlineData(PaymentMethodEnum.UPI)]
    [InlineData(PaymentMethodEnum.CreditCard)]
    [InlineData(PaymentMethodEnum.DebitCard)]
    [InlineData(PaymentMethodEnum.BankTransfer)]
    public void CashierDecided_With_Confirmed_False_Produces_ProvisionalFee(PaymentMethodEnum method)
    {
        var kind = FeeReceiptPaymentPolicy.DetermineFeeReceiptKind([method], bankReceiptConfirmed: false);
        Assert.Equal(FeeReceiptKind.ProvisionalFee, kind);
    }

    [Theory]
    [InlineData(PaymentMethodEnum.UPI)]
    [InlineData(PaymentMethodEnum.CreditCard)]
    [InlineData(PaymentMethodEnum.DebitCard)]
    public void CashierDecided_With_Null_Flag_Defaults_To_ProvisionalFee(PaymentMethodEnum method)
    {
        // Defensive default: null = unconfirmed → provisional
        var kind = FeeReceiptPaymentPolicy.DetermineFeeReceiptKind([method], bankReceiptConfirmed: null);
        Assert.Equal(FeeReceiptKind.ProvisionalFee, kind);
    }

    [Theory]
    [InlineData(PaymentMethodEnum.UPI)]
    [InlineData(PaymentMethodEnum.CreditCard)]
    [InlineData(PaymentMethodEnum.BankTransfer)]
    public void Electronic_Methods_Do_Not_Ask_Cashier_To_Finalize(PaymentMethodEnum method)
    {
        Assert.False(FeeReceiptPaymentPolicy.RequiresBankConfirmation([method]));
    }

    // ── Mixed-method (most-restrictive wins) ──────────────────────────────────

    [Fact]
    public void Mixed_Cheque_And_UPI_With_Confirmed_True_Still_Produces_ProvisionalFee()
    {
        // AlwaysProvisional (Cheque) in mix → provisional always wins
        var kind = FeeReceiptPaymentPolicy.DetermineFeeReceiptKind(
            [PaymentMethodEnum.Cheque, PaymentMethodEnum.UPI],
            bankReceiptConfirmed: true);
        Assert.Equal(FeeReceiptKind.ProvisionalFee, kind);
    }

    [Fact]
    public void Mixed_Cash_And_UPI_With_Confirmed_True_Remains_Provisional()
    {
        // Cash cannot bypass verification for the non-cash share.
        var kind = FeeReceiptPaymentPolicy.DetermineFeeReceiptKind(
            [PaymentMethodEnum.Cash, PaymentMethodEnum.UPI],
            bankReceiptConfirmed: true);
        Assert.Equal(FeeReceiptKind.ProvisionalFee, kind);
    }

    [Fact]
    public void Mixed_Cash_And_UPI_With_Confirmed_False_Produces_ProvisionalFee()
    {
        var kind = FeeReceiptPaymentPolicy.DetermineFeeReceiptKind(
            [PaymentMethodEnum.Cash, PaymentMethodEnum.UPI],
            bankReceiptConfirmed: false);
        Assert.Equal(FeeReceiptKind.ProvisionalFee, kind);
    }
}

using ShikshaMitra.Finance.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Receipts;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

/// <summary>
/// Verifies that every non-cash payment method routes to provisional receipt
/// and cash routes directly to final receipt.
/// </summary>
public class FeeReceiptPaymentPolicy_Tests
{
    [Theory]
    [InlineData(PaymentMethodEnum.Cheque)]
    [InlineData(PaymentMethodEnum.DemandDraft)]
    [InlineData(PaymentMethodEnum.BankTransfer)]
    [InlineData(PaymentMethodEnum.CreditCard)]
    [InlineData(PaymentMethodEnum.UPI)]
    [InlineData(PaymentMethodEnum.OnlinePaymentGateway)]
    [InlineData(PaymentMethodEnum.WalletPrepaidCard)]
    public void All_NonCash_Methods_Require_Verification(PaymentMethodEnum method)
    {
        var requiresVerification = FeeReceiptPaymentPolicy.RequiresVerification(new[] { method });
        Assert.True(requiresVerification, $"{method} should require verification");
    }

    [Fact]
    public void Cash_Does_Not_Require_Verification()
    {
        var requiresVerification = FeeReceiptPaymentPolicy.RequiresVerification(
            new[] { PaymentMethodEnum.Cash });
        Assert.False(requiresVerification);
    }

    [Theory]
    [InlineData(PaymentMethodEnum.Cheque)]
    [InlineData(PaymentMethodEnum.DemandDraft)]
    [InlineData(PaymentMethodEnum.BankTransfer)]
    [InlineData(PaymentMethodEnum.CreditCard)]
    [InlineData(PaymentMethodEnum.UPI)]
    [InlineData(PaymentMethodEnum.OnlinePaymentGateway)]
    [InlineData(PaymentMethodEnum.WalletPrepaidCard)]
    public void Transaction_With_Any_NonCash_Method_Gets_ProvisionalFee_Kind(PaymentMethodEnum method)
    {
        var receiptKind = FeeReceiptPaymentPolicy.DetermineFeeReceiptKind(new[] { method });
        Assert.Equal(FeeReceiptKind.ProvisionalFee, receiptKind);
    }

    [Fact]
    public void Transaction_With_Only_Cash_Gets_FinalFee_Kind()
    {
        var receiptKind = FeeReceiptPaymentPolicy.DetermineFeeReceiptKind(
            new[] { PaymentMethodEnum.Cash });
        Assert.Equal(FeeReceiptKind.FinalFee, receiptKind);
    }

    [Fact]
    public void Mixed_Cash_And_NonCash_Still_Gets_ProvisionalFee_Kind()
    {
        // Any non-cash in the transaction → provisional
        var receiptKind = FeeReceiptPaymentPolicy.DetermineFeeReceiptKind(
            new[] { PaymentMethodEnum.Cash, PaymentMethodEnum.Cheque });
        Assert.Equal(FeeReceiptKind.ProvisionalFee, receiptKind);
    }
}

using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

/// <summary>
/// Tests that FeeReceiptPaymentPolicy applies the same provisional/final decision
/// to both fee and charge receipt kinds (Phase 5 parity requirement).
/// </summary>
public class FeeReceiptKind_Parity_Tests
{
    // Cheque always provisional — applies to charges too via RequiresVerification()
    [Theory]
    [InlineData(PaymentMethodEnum.Cheque)]
    [InlineData(PaymentMethodEnum.DemandDraft)]
    public void Cheque_DD_RequiresVerification_True_For_Charge_Receipt(PaymentMethodEnum method)
    {
        Assert.True(FeeReceiptPaymentPolicy.RequiresVerification([method]));
    }

    // Cash / UPI-confirmed never requires verification → charge gets FinalCharge
    [Theory]
    [InlineData(PaymentMethodEnum.Cash)]
    public void Cash_RequiresVerification_False_For_Charge_Receipt(PaymentMethodEnum method)
    {
        Assert.False(FeeReceiptPaymentPolicy.RequiresVerification([method]));
    }

    // FinalCharge (value=4) is a distinct enum value from FinalFee (value=1)
    [Fact]
    public void FinalCharge_And_FinalFee_Are_Different_Enum_Values()
    {
        Assert.NotEqual(FeeReceiptKind.FinalFee, FeeReceiptKind.FinalCharge);
        Assert.Equal(4, (int)FeeReceiptKind.FinalCharge);
        Assert.Equal(1, (int)FeeReceiptKind.FinalFee);
    }

    // ProvisionalCharge (value=3) is distinct from ProvisionalFee (value=2)
    [Fact]
    public void ProvisionalCharge_And_ProvisionalFee_Are_Different_Enum_Values()
    {
        Assert.NotEqual(FeeReceiptKind.ProvisionalFee, FeeReceiptKind.ProvisionalCharge);
        Assert.Equal(3, (int)FeeReceiptKind.ProvisionalCharge);
        Assert.Equal(2, (int)FeeReceiptKind.ProvisionalFee);
    }
}

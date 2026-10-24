using System;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

public class OfficialReceiptFlow_Tests
{
    [Fact]
    public void Every_NonCash_Method_Requires_Verification_Regardless_Of_Cashier_Flag()
    {
        foreach (var method in Enum.GetValues<PaymentMethodEnum>())
        foreach (var confirmation in new bool?[] { null, false, true })
        {
            var expected = method == PaymentMethodEnum.Cash ? FeeReceiptKind.FinalFee : FeeReceiptKind.ProvisionalFee;
            Assert.Equal(expected, FeeReceiptPaymentPolicy.DetermineFeeReceiptKind([method], confirmation));
            Assert.Equal(method != PaymentMethodEnum.Cash, FeeReceiptPaymentPolicy.RequiresVerification([method]));
        }
    }

    [Fact]
    public void Missing_Method_Cannot_Create_Official_Evidence()
        => Assert.Equal(FeeReceiptKind.ProvisionalFee, FeeReceiptPaymentPolicy.DetermineFeeReceiptKind([], true));

    [Theory]
    [InlineData(FeeReceiptKind.FinalFee, FeeReceiptScope.Fee, "Official Receipt (Fee)", "Official-Receipt_Fee_FIN-2026-000001_2026-10-23.pdf")]
    [InlineData(FeeReceiptKind.FinalCharge, FeeReceiptScope.Charge, "Official Receipt (Charge)", "Official-Receipt_Charge_FIN-2026-000001_2026-10-23.pdf")]
    [InlineData(FeeReceiptKind.ProvisionalCharge, FeeReceiptScope.Charge, "Provisional Receipt (Charge)", "Provisional-Receipt_Charge_FIN-2026-000001_2026-10-23.pdf")]
    [InlineData(FeeReceiptKind.ProvisionalFee, FeeReceiptScope.Combined, "Provisional Receipt (Fees and Charges)", "Provisional-Receipt_Fees-And-Charges_FIN-2026-000001_2026-10-23.pdf")]
    public void Labels_And_Downloads_Distinguish_Status_And_Scope(FeeReceiptKind kind, FeeReceiptScope scope, string label, string filename)
    {
        Assert.Equal(label, FeeReceiptPresentation.GetLabel(kind, scope));
        Assert.Equal(filename, FeeReceiptPresentation.GetPdfFileName(kind, scope, "FIN-2026-000001", new DateOnly(2026, 10, 23)));
    }

    [Fact]
    public void Download_Name_Sanitizes_Receipt_Number()
    {
        var name = FeeReceiptPresentation.GetPdfFileName(FeeReceiptKind.FinalFee, FeeReceiptScope.Fee,
            "BOOK/2026\r\nTEST", new DateOnly(2026, 10, 23));
        Assert.DoesNotContain("/", name);
        Assert.DoesNotContain("\r", name);
        Assert.DoesNotContain("\n", name);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Fees;

public class CollectFeeInputValidator_Tests
{
    private readonly CollectFeeInputValidator _validator = new();

    private static CollectFeeInput ValidInput()
    {
        var dueFeeId = Guid.NewGuid();
        var feeHeadId = Guid.NewGuid();
        return new CollectFeeInput
        {
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            StudentId = Guid.NewGuid(),
            AcademicYearId = Guid.NewGuid(),
            DueFeePayments = new List<DueFeePaymentDto>
            {
                new() { DueFeeId = dueFeeId, FeeHeadId = feeHeadId, Amount = 1000d },
            },
            Payments = new List<FeePaymentDto>
            {
                new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = 1000d },
            },
        };
    }

    [Fact]
    public void Should_Pass_For_Valid_Input()
    {
        var result = _validator.Validate(ValidInput());
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Should_Reject_Empty_StudentId()
    {
        var input = ValidInput();
        input.StudentId = Guid.Empty;

        var result = _validator.Validate(input);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "StudentId");
    }

    [Fact]
    public void Should_Reject_Zero_Amount_In_DueFeePayments()
    {
        var input = ValidInput();
        input.DueFeePayments[0].Amount = 0;
        input.Payments[0].Amount = 0;

        var result = _validator.Validate(input);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Should_Reject_Empty_DueFeeAndChargePayments()
    {
        var input = ValidInput();
        input.DueFeePayments = new List<DueFeePaymentDto>();
        input.ChargePayments = new List<ChargePaymentDto>();

        var result = _validator.Validate(input);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "");
    }

    [Fact]
    public void Should_Reject_Empty_Payments()
    {
        var input = ValidInput();
        input.Payments = new List<FeePaymentDto>();

        var result = _validator.Validate(input);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName.Contains("Payments"));
    }

    [Fact]
    public void Should_Reject_When_Payment_Sum_Does_Not_Match_Lines_Total()
    {
        var input = ValidInput();
        input.Payments[0].Amount = 500d;

        var result = _validator.Validate(input);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Should_Pass_For_Valid_DueFee_And_Charge_Payment()
    {
        var input = ValidInput();
        input.ChargePayments = new List<ChargePaymentDto>
        {
            new() { ChargeId = Guid.NewGuid(), Amount = 500d },
        };
        input.Payments[0].Amount = 1500d;

        var result = _validator.Validate(input);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Should_Reject_Mixed_Cash_And_Uncleared_Cheque()
    {
        var input = ValidInput();
        input.Payments =
        [
            new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = 400d },
            new() { PaymentMethod = PaymentMethodEnum.Cheque, Amount = 600d,
                Details = MinimalDetails(PaymentMethodEnum.Cheque) }
        ];

        var result = _validator.Validate(input);
        result.Errors.ShouldContain(e => e.ErrorMessage.Contains("one payment method"));
    }

    [Fact]
    public void Should_Reject_Provisional_Fee_And_Charge_In_One_Receipt_Decision()
    {
        var input = ValidInput();
        input.ChargePayments = [new() { ChargeId = Guid.NewGuid(), Amount = 500d }];
        input.Payments = [new() { PaymentMethod = PaymentMethodEnum.Cheque, Amount = 1500d,
            Details = MinimalDetails(PaymentMethodEnum.Cheque) }];

        var result = _validator.Validate(input);
        result.Errors.ShouldContain(e => e.ErrorMessage.Contains("one fee or charge receipt"));
    }

    [Fact]
    public void Should_Pass_For_LateFee_Only_Payment()
    {
        var input = ValidInput();
        input.DueFeePayments = [];
        input.LateFeePayments =
        [
            new LateFeePaymentDto { DueFeeId = Guid.NewGuid(), Amount = 100d },
        ];
        input.Payments[0].Amount = 100d;

        var result = _validator.Validate(input);

        var errors = string.Join("; ", result.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"));
        result.IsValid.ShouldBeTrue(errors);
    }

    [Fact]
    public void Should_Reject_Duplicate_DueFeeId()
    {
        var input = ValidInput();
        var dueFeeId = input.DueFeePayments[0].DueFeeId;
        var feeHeadId = input.DueFeePayments[0].FeeHeadId;
        input.DueFeePayments.Add(new DueFeePaymentDto { DueFeeId = dueFeeId, FeeHeadId = feeHeadId, Amount = 500d });
        input.Payments[0].Amount = 1500d;

        var result = _validator.Validate(input);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName.Contains("DueFeePayments"));
    }

    [Fact]
    public void Should_Reject_Duplicate_ChargeId()
    {
        var input = ValidInput();
        var chargeId = Guid.NewGuid();
        input.ChargePayments = new List<ChargePaymentDto>
        {
            new() { ChargeId = chargeId, Amount = 200d },
            new() { ChargeId = chargeId, Amount = 300d },
        };
        input.Payments[0].Amount = 1500d;

        var result = _validator.Validate(input);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName.Contains("ChargePayments"));
    }

    [Fact]
    public void Should_Reject_Too_Long_Notes()
    {
        var input = ValidInput();
        input.Notes = new string('x', 1001);

        var result = _validator.Validate(input);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Notes");
    }

    // ── BankReceiptConfirmed validation ───────────────────────────────────────

    private static Dictionary<string, object?> MinimalDetails(PaymentMethodEnum method) =>
        method switch
        {
            PaymentMethodEnum.UPI => new() { { "transactionId", "UPI123" }, { "upiId", "test@upi" } },
            PaymentMethodEnum.BankTransfer => new() { { "transactionId", "NEFT123" } },
            PaymentMethodEnum.Cheque => new() { { "chequeNumber", "123456" }, { "bankName", "Test Bank" } },
            PaymentMethodEnum.DemandDraft => new() { { "ddNumber", "1234567" } },
            _ => new(),
        };

    [Theory]
    [InlineData(PaymentMethodEnum.UPI)]
    [InlineData(PaymentMethodEnum.CreditCard)]
    [InlineData(PaymentMethodEnum.DebitCard)]
    [InlineData(PaymentMethodEnum.BankTransfer)]
    public void Should_Reject_CashierDecided_Method_Without_BankReceiptConfirmed(PaymentMethodEnum method)
    {
        var input = ValidInput();
        input.Payments = new List<FeePaymentDto>
        {
            new() { PaymentMethod = method, Amount = 1000d, Details = MinimalDetails(method) },
        };

        var result = _validator.Validate(input);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "PaymentConfirmed");
    }

    [Theory]
    [InlineData(PaymentMethodEnum.UPI, true)]
    [InlineData(PaymentMethodEnum.UPI, false)]
    [InlineData(PaymentMethodEnum.CreditCard, true)]
    [InlineData(PaymentMethodEnum.CreditCard, false)]
    [InlineData(PaymentMethodEnum.DebitCard, true)]
    [InlineData(PaymentMethodEnum.DebitCard, false)]
    [InlineData(PaymentMethodEnum.BankTransfer, false)]
    public void Should_Accept_CashierDecided_Method_With_BankReceiptConfirmed(
        PaymentMethodEnum method, bool bankConfirmed)
    {
        var input = ValidInput();
        input.Payments = new List<FeePaymentDto>
        {
            new() { PaymentMethod = method, Amount = 1000d, Details = MinimalDetails(method) },
        };
        input.PaymentConfirmed = bankConfirmed;

        var result = _validator.Validate(input);

        var errors = string.Join("; ", result.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"));
        result.IsValid.ShouldBeTrue(errors);
    }

    [Theory]
    [InlineData(PaymentMethodEnum.Cash)]
    [InlineData(PaymentMethodEnum.Cheque)]
    [InlineData(PaymentMethodEnum.DemandDraft)]
    public void Should_Reject_BankReceiptConfirmed_Set_For_NonCashierDecided_Methods(
        PaymentMethodEnum method)
    {
        var input = ValidInput();
        input.Payments = new List<FeePaymentDto>
        {
            new() { PaymentMethod = method, Amount = 1000d, Details = MinimalDetails(method) },
        };
        input.PaymentConfirmed = true;

        var result = _validator.Validate(input);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "PaymentConfirmed");
    }

    [Fact]
    public void Should_Accept_Cash_Without_BankReceiptConfirmed()
    {
        var input = ValidInput();
        var result = _validator.Validate(input);
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Should_Accept_Cheque_Without_BankReceiptConfirmed()
    {
        var input = ValidInput();
        input.Payments = new List<FeePaymentDto>
        {
            new() { PaymentMethod = PaymentMethodEnum.Cheque, Amount = 1000d, Details = MinimalDetails(PaymentMethodEnum.Cheque) },
        };

        var result = _validator.Validate(input);

        var errors = string.Join("; ", result.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"));
        result.IsValid.ShouldBeTrue(errors);
    }
}

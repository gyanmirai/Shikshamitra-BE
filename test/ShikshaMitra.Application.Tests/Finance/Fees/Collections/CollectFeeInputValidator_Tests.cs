using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Finance.FeeCollection;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Fees;

public class CollectFeeInputValidator_Tests
{
    private readonly CollectFeeInputValidator _validator = new();

    private static CollectFeeInput ValidInput() => new()
    {
        StudentId = Guid.NewGuid(),
        AcademicYearId = Guid.NewGuid(),
        TotalAmount = 1000d,
        SelectedFeeHeads = new List<SelectedFeeHeadDto>
        {
            new() { FeeHeadId = Guid.NewGuid(), Amount = 1000d },
        },
        Payments = new List<FeePaymentDto>
        {
            new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = 1000d },
        },
    };

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
    public void Should_Reject_Zero_TotalAmount()
    {
        var input = ValidInput();
        input.TotalAmount = 0;
        input.Payments[0].Amount = 0;

        var result = _validator.Validate(input);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "TotalAmount");
    }

    [Fact]
    public void Should_Reject_Empty_FeeHeads()
    {
        var input = ValidInput();
        input.SelectedFeeHeads = new List<SelectedFeeHeadDto>();

        var result = _validator.Validate(input);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName.Contains("SelectedFeeHeads"));
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
    public void Should_Reject_When_Payment_Sum_Does_Not_Match_Total()
    {
        var input = ValidInput();
        // Total 1000, received (null) defaults to 1000, but payment is 500
        input.Payments[0].Amount = 500d;

        var result = _validator.Validate(input);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Should_Accept_Partial_Payment_When_ReceivedAmount_Matches()
    {
        var input = ValidInput();
        input.TotalAmount = 1000d;
        input.ReceivedAmount = 500d;
        input.Payments[0].Amount = 500d;

        var result = _validator.Validate(input);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Should_Reject_Allocation_Sum_Mismatch()
    {
        var input = ValidInput();
        var feeHeadId = input.SelectedFeeHeads[0].FeeHeadId;
        input.SelectedFeeHeads[0].Allocations = new List<DueFeeAllocationDto>
        {
            new() { DueFeeId = Guid.NewGuid(), Amount = 300d }, // should be 1000
        };

        var result = _validator.Validate(input);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Should_Pass_When_Allocation_Sum_Matches_FeeHead_Amount()
    {
        var input = ValidInput();
        input.SelectedFeeHeads[0].Allocations = new List<DueFeeAllocationDto>
        {
            new() { DueFeeId = Guid.NewGuid(), Amount = 600d },
            new() { DueFeeId = Guid.NewGuid(), Amount = 400d },
        };

        var result = _validator.Validate(input);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Should_Reject_FeeHead_With_Zero_Amount()
    {
        var input = ValidInput();
        input.SelectedFeeHeads[0].Amount = 0d;

        var result = _validator.Validate(input);

        result.IsValid.ShouldBeFalse();
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

    // Helper: builds minimal required details for methods that need them
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
    [InlineData(PaymentMethodEnum.OnlinePaymentGateway)]
    [InlineData(PaymentMethodEnum.WalletPrepaidCard)]
    public void Should_Reject_CashierDecided_Method_Without_BankReceiptConfirmed(PaymentMethodEnum method)
    {
        var input = ValidInput();
        input.Payments = new List<FeePaymentDto>
        {
            new() { PaymentMethod = method, Amount = 1000d, Details = MinimalDetails(method) },
        };
        // BankReceiptConfirmed not set (null) → should fail

        var result = _validator.Validate(input);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "BankReceiptConfirmed");
    }

    [Theory]
    [InlineData(PaymentMethodEnum.UPI, true)]
    [InlineData(PaymentMethodEnum.UPI, false)]
    [InlineData(PaymentMethodEnum.CreditCard, true)]
    [InlineData(PaymentMethodEnum.CreditCard, false)]
    [InlineData(PaymentMethodEnum.DebitCard, true)]
    [InlineData(PaymentMethodEnum.DebitCard, false)]
    [InlineData(PaymentMethodEnum.BankTransfer, false)]
    [InlineData(PaymentMethodEnum.OnlinePaymentGateway, true)]
    [InlineData(PaymentMethodEnum.OnlinePaymentGateway, false)]
    [InlineData(PaymentMethodEnum.WalletPrepaidCard, true)]
    [InlineData(PaymentMethodEnum.WalletPrepaidCard, false)]
    public void Should_Accept_CashierDecided_Method_With_BankReceiptConfirmed(
        PaymentMethodEnum method, bool bankConfirmed)
    {
        var input = ValidInput();
        input.Payments = new List<FeePaymentDto>
        {
            new() { PaymentMethod = method, Amount = 1000d, Details = MinimalDetails(method) },
        };
        input.BankReceiptConfirmed = bankConfirmed;

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
        input.BankReceiptConfirmed = true; // must be null for Cash/Cheque/DD

        var result = _validator.Validate(input);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "BankReceiptConfirmed");
    }

    [Fact]
    public void Should_Accept_Cash_Without_BankReceiptConfirmed()
    {
        var input = ValidInput(); // Cash + no BankReceiptConfirmed
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
        // BankReceiptConfirmed = null (not set) → OK for Cheque

        var result = _validator.Validate(input);

        var errors = string.Join("; ", result.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"));
        result.IsValid.ShouldBeTrue(errors);
    }
}

using System;
using System.Collections.Generic;
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
}

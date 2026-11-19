using System;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Fees;

public class ApplicantRefundInput_Tests
{
    private static CreateFeeRefundRequestDto Input() => new()
    {
        FinanceFeeAccountId = Guid.NewGuid(), AcademicYearId = Guid.NewGuid(), OriginalReceiptId = Guid.NewGuid(),
        IdempotencyKey = Guid.NewGuid().ToString("N"), RefundMethod = "Cash",
        PaymentDetails = new() { ["receivedBy"] = "QA Parent" },
        Lines = [new() { DueFeeId = Guid.NewGuid(), SourceAllocationId = Guid.NewGuid(), Amount = 123.45m }]
    };

    [Fact]
    public void Account_receipt_refund_requires_no_dummy_student()
    {
        var input = Input();
        input.StudentId.ShouldBeNull();
        new CreateFeeRefundRequestDtoValidator().Validate(input).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Legacy_student_refund_keeps_existing_contract()
    {
        var input = Input();
        input.FinanceFeeAccountId = null;
        input.StudentId = Guid.NewGuid();
        new CreateFeeRefundRequestDtoValidator().Validate(input).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Refund_rejects_missing_payer_and_empty_identity()
    {
        var input = Input();
        input.FinanceFeeAccountId = null;
        new CreateFeeRefundRequestDtoValidator().Validate(input).IsValid.ShouldBeFalse();
        input.FinanceFeeAccountId = Guid.Empty;
        new CreateFeeRefundRequestDtoValidator().Validate(input).IsValid.ShouldBeFalse();
        input.FinanceFeeAccountId = Guid.NewGuid();
        input.StudentId = Guid.Empty;
        new CreateFeeRefundRequestDtoValidator().Validate(input).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Refund_still_requires_source_allocation_and_beneficiary()
    {
        var input = Input();
        input.Lines[0].SourceAllocationId = Guid.Empty;
        new CreateFeeRefundRequestDtoValidator().Validate(input).IsValid.ShouldBeFalse();
        input.Lines[0].SourceAllocationId = Guid.NewGuid();
        input.PaymentDetails!.Clear();
        new CreateFeeRefundRequestDtoValidator().Validate(input).IsValid.ShouldBeFalse();
    }
}

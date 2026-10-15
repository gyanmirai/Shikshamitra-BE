using System;
using System.Text.Json;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Shouldly;
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.Finance.Fees;

public class AdmissionFeeCollectionInputMapper_Tests
{
    private static FinanceFeeAccount Account() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "QA Applicant");

    private static CollectAdmissionFeeInput Valid(decimal amount = 123.45m) => new()
    {
        IdempotencyKey = Guid.NewGuid().ToString("N"),
        DueFeePayments = [new() { DueFeeId = Guid.NewGuid(), FeeHeadId = Guid.NewGuid(), Amount = amount }],
        Payments = [new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = amount }],
    };

    [Fact]
    public void Unenrolled_applicant_uses_verified_account_without_fake_student()
    {
        var account = Account();
        var mapped = AdmissionFeeCollectionInputMapper.Map(Valid(), account);
        mapped.StudentId.ShouldBeNull();
        mapped.FinanceFeeAccountId.ShouldBe(account.Id);
        mapped.AcademicYearId.ShouldBe(account.AcademicYearId);
        mapped.DueFeePayments[0].Amount.ShouldBe(123.45d);
        new CollectFeeInputValidator().Validate(mapped).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Linked_student_identity_is_derived_from_account()
    {
        var account = Account();
        var studentId = Guid.NewGuid();
        account.LinkStudent(studentId, DateTimeOffset.UtcNow);
        AdmissionFeeCollectionInputMapper.Map(Valid(), account).StudentId.ShouldBe(studentId);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("123.456")]
    [InlineData("1000000000000.00")]
    public void Rejects_invalid_money_before_legacy_double_conversion(string amount)
    {
        var input = Valid(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture));
        Should.Throw<AbpValidationException>(() => AdmissionFeeCollectionInputMapper.Map(input, Account()));
    }

    [Fact]
    public void Rejects_duplicate_due_allocation()
    {
        var input = Valid();
        input.DueFeePayments.Add(input.DueFeePayments[0]);
        input.Payments[0].Amount *= 2;
        Should.Throw<AbpValidationException>(() => AdmissionFeeCollectionInputMapper.Map(input, Account()));
    }

    [Fact]
    public void Rejects_payment_and_allocation_mismatch()
    {
        var input = Valid();
        input.Payments[0].Amount -= 0.01m;
        Should.Throw<AbpValidationException>(() => AdmissionFeeCollectionInputMapper.Map(input, Account()));
    }

    [Fact]
    public void Accepts_split_counter_instruments_with_exact_total()
    {
        var input = Valid();
        input.Payments[0].Amount = 100m;
        input.Payments.Add(new() { PaymentMethod = PaymentMethodEnum.UPI, Amount = 23.45m });
        var mapped = AdmissionFeeCollectionInputMapper.Map(input, Account());
        mapped.Payments.Count.ShouldBe(2);
        mapped.Payments[1].Amount.ShouldBe(23.45d);
    }

    [Fact]
    public void Client_cannot_spoof_internal_account_on_existing_student_endpoint()
    {
        var json = JsonSerializer.Serialize(new { FinanceFeeAccountId = Guid.NewGuid(), AcademicYearId = Guid.NewGuid(),
            IdempotencyKey = "retry-key", DueFeePayments = new[] { new { DueFeeId = Guid.NewGuid(), FeeHeadId = Guid.NewGuid(), Amount = 100 } },
            Payments = new[] { new { PaymentMethod = PaymentMethodEnum.Cash, Amount = 100 } } });
        var input = JsonSerializer.Deserialize<CollectFeeInput>(json)!;
        input.FinanceFeeAccountId.ShouldBeNull();
        new CollectFeeInputValidator().Validate(input).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Rejects_missing_lists_and_unknown_instrument()
    {
        var input = Valid();
        input.Payments[0].PaymentMethod = (PaymentMethodEnum)999;
        Should.Throw<AbpValidationException>(() => AdmissionFeeCollectionInputMapper.Map(input, Account()));
        input = Valid();
        input.Payments = null!;
        Should.Throw<AbpValidationException>(() => AdmissionFeeCollectionInputMapper.Map(input, Account()));
    }
}

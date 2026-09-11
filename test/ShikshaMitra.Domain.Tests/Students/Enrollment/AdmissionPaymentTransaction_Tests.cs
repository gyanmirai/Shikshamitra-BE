using System;
using ShikshaMitra.Finance.Admissions;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionPaymentTransaction_Tests
{
	private readonly Guid _collector = Guid.NewGuid();
	private readonly DateTime _now = new(2026, 9, 11, 8, 0, 0, DateTimeKind.Utc);

	[Fact]
	public void Cash_Can_Settle_Immediately_Without_A_Reference()
	{
		var payment = Staff("cash", null, independent: false);
		payment.Status.ShouldBe(AdmissionPaymentStatuses.Settled);
		payment.VerifiedBy.ShouldBe(_collector);
	}

	[Fact]
	public void Non_Cash_Requires_A_Reference() =>
		Should.Throw<BusinessException>(() => Staff("upi", null, independent: false));

	[Fact]
	public void Independent_Verification_Requires_A_Different_Actor()
	{
		var payment = Staff("upi", "UPI-1", independent: true);
		Should.Throw<BusinessException>(() => payment.Verify(true, null, "verify-1", _collector, _now));
		payment.Verify(true, "Matched bank statement", "verify-1", Guid.NewGuid(), _now.AddMinutes(2));
		payment.Status.ShouldBe(AdmissionPaymentStatuses.Settled);
	}

	[Fact]
	public void Rejection_Requires_A_Reason_And_Is_Final()
	{
		var payment = Staff("cheque", "CHQ-1", independent: true);
		Should.Throw<ArgumentException>(() => payment.Verify(false, null, "reject-1", Guid.NewGuid(), _now));
		payment.Verify(false, "Cheque details do not match.", "reject-1", Guid.NewGuid(), _now);
		payment.Status.ShouldBe(AdmissionPaymentStatuses.Rejected);
		Should.Throw<BusinessException>(() => payment.Verify(true, null, "verify-1", Guid.NewGuid(), _now));
	}

	[Fact]
	public void Online_Settlement_Is_Idempotent_For_The_Same_Provider_Reference()
	{
		var payment = Online();
		payment.SettleOnline("gateway", "txn-1", "APR-1", _now);
		payment.SettleOnline("gateway", "txn-1", "APR-1", _now.AddMinutes(1));
		payment.Status.ShouldBe(AdmissionPaymentStatuses.Settled);
		Should.Throw<BusinessException>(() => payment.SettleOnline("gateway", "txn-2", "APR-2", _now));
	}

	[Fact]
	public void Failed_Online_Attempt_Cannot_Be_Settled_Later()
	{
		var payment = Online();
		payment.FailOnline("Provider declined the payment.", _now);
		payment.Status.ShouldBe(AdmissionPaymentStatuses.Failed);
		Should.Throw<BusinessException>(() => payment.SettleOnline("gateway", "txn", "APR", _now));
	}

	[Fact]
	public void Reversal_Preserves_The_Original_Payment_Evidence()
	{
		var payment = Staff("upi", "UPI-1", independent: false);
		payment.Reverse("Bank reversal confirmed.", "reverse-1", Guid.NewGuid(), _now.AddDays(1));
		payment.Status.ShouldBe(AdmissionPaymentStatuses.Reversed);
		payment.ExternalReference.ShouldBe("UPI-1");
		payment.Amount.ShouldBe(500m);
	}

	[Fact]
	public void Waiver_Is_An_Explicit_Reasoned_Record()
	{
		var payment = AdmissionPaymentTransaction.RecordWaiver(Guid.NewGuid(), Guid.NewGuid(),
			Guid.NewGuid(), Guid.NewGuid(), 1, "APPLICATION_FEE", "INR",
			"Principal-approved hardship waiver.", "{}", "waive-1",
			_collector, _now);
		payment.Status.ShouldBe(AdmissionPaymentStatuses.Waived);
		payment.Amount.ShouldBe(0m);
		payment.DecisionReason.ShouldBe("Principal-approved hardship waiver.");
	}

	private AdmissionPaymentTransaction Staff(string method, string? reference, bool independent) =>
		AdmissionPaymentTransaction.RecordStaffPayment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			Guid.NewGuid(), 1, 500m, "APPLICATION_FEE", "INR", method, reference, "APR-1", null, "{}", "staff-1", _collector,
			_now, independent);

	private AdmissionPaymentTransaction Online() => AdmissionPaymentTransaction.CreateOnlineIntent(
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 500m, "APPLICATION_FEE", "INR", "{}", "online-1",
		_collector, _now);
}

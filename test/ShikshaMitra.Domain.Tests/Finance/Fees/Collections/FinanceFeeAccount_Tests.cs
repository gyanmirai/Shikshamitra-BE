using System;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.FeeRefund;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Collections;

public class FinanceFeeAccount_Tests
{
	private static FinanceFeeAccount CreateAccount() => new(Guid.NewGuid(), Guid.NewGuid(),
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
		Guid.NewGuid(), Guid.NewGuid(), "Kabir Shah");

	[Fact]
	public void Applicant_Identity_And_Assignment_Evidence_Survive_Idempotent_Student_Link()
	{
		var account = CreateAccount();
		var application = account.ApplicationId;
		var originalName = account.ApplicantName;
		var structure = Guid.NewGuid();
		account.CaptureAssignment(structure, 2, "{\"dueIds\":[\"original\"]}", new string('a', 64));
		account.CaptureAssignment(structure, 2, "{\"dueIds\":[\"retry\"]}", new string('a', 64));
		var student = Guid.NewGuid();
		var linkedAt = new DateTimeOffset(2026, 10, 12, 10, 30, 0, TimeSpan.FromHours(5.5));
		account.LinkStudent(student, linkedAt);
		account.LinkStudent(student, linkedAt.AddDays(1));
		var assignment = Guid.NewGuid();
		account.BindStudentAssignment(assignment);
		account.BindStudentAssignment(assignment);
		Assert.Equal(student, account.StudentId);
		Assert.Equal(linkedAt.ToUniversalTime(), account.StudentLinkedAt);
		Assert.Equal(TimeSpan.Zero, account.StudentLinkedAt!.Value.Offset);
		Assert.Equal(application, account.ApplicationId);
		Assert.Equal(originalName, account.ApplicantName);
		Assert.Equal("{\"dueIds\":[\"original\"]}", account.AssignmentSnapshotJson);
		Assert.Equal(assignment, account.StudentFeeStructureId);
		Assert.Equal(FinanceFeeAccountErrorCodes.StudentLinkConflict,
			Assert.Throws<BusinessException>(() => account.LinkStudent(Guid.NewGuid(), linkedAt)).Code);
		Assert.Equal(FinanceFeeAccountErrorCodes.AssignmentConflict,
			Assert.Throws<BusinessException>(() => account.BindStudentAssignment(Guid.NewGuid())).Code);
	}

	[Fact]
	public void Conflicting_Assignment_Cannot_Replace_Financial_Evidence()
	{
		var account = CreateAccount();
		var structure = Guid.NewGuid();
		account.CaptureAssignment(structure, 1, "original", new string('a', 64));
		Assert.Throws<BusinessException>(() => account.CaptureAssignment(structure, 1, "changed", new string('b', 64)));
		Assert.Throws<BusinessException>(() => account.CaptureAssignment(structure, 2, "changed", new string('a', 64)));
		Assert.Throws<BusinessException>(() => account.BindStudentAssignment(Guid.NewGuid()));
		Assert.Equal("original", account.AssignmentSnapshotJson);
	}

	[Fact]
	public void Handoff_Retains_Due_Id_Payments_Balances_And_Schedule_Instead_Of_Regenerating()
	{
		var account = CreateAccount();
		var due = new DueFee(Guid.NewGuid(), account.TenantId, null, Guid.NewGuid(),
			MonthEnum.October, 2026, true, 2500m, new DateTime(2026, 10, 12), financeFeeAccountId: account.Id);
		due.ApplyPayment(600m);
		due.ApplyRefund(100m);
		var identity = due.Id;
		var scheduleKey = due.ScheduleInstanceKey;
		var student = Guid.NewGuid();
		var assignment = Guid.NewGuid();
		due.LinkStudent(student);
		due.LinkStudent(student);
		due.BindAssignment(assignment);
		due.BindAssignment(assignment);
		Assert.Equal(identity, due.Id);
		Assert.Equal(account.Id, due.FinanceFeeAccountId);
		Assert.Equal(student, due.StudentId);
		Assert.Equal(assignment, due.StudentFeeStructureId);
		Assert.Equal(scheduleKey, due.ScheduleInstanceKey);
		Assert.Equal(2500m, due.OriginalAmount);
		Assert.Equal(600m, due.PaidAmount);
		Assert.Equal(100m, due.RefundedAmount);
		Assert.Equal(2000m, due.OutstandingAmount);
		Assert.Throws<BusinessException>(() => due.LinkStudent(Guid.NewGuid()));
	}

	[Fact]
	public void Metadata_Payment_Record_Receipt_And_Allocation_References_Survive_Handoff()
	{
		var account = CreateAccount();
		var metadata = new FeeRecordMetadata(Guid.NewGuid(), account.TenantId, null, account.AcademicYearId,
			500, "REC-2026-1", FeeCollectionStatus.Paid, financeFeeAccountId: account.Id);
		var payment = PaymentMethodRecord.Create(null, metadata.Id, 500m, DateTimeOffset.UtcNow,
			PaymentMethodEnum.Cash, tenantId: account.TenantId, financeFeeAccountId: account.Id);
		var record = new FeeCollectionRecord(Guid.NewGuid())
		{
			StudentId = null, Amount = 500, FeeRecordMetadataId = metadata.Id,
		};
		record.SetFinanceFeeAccount(account.Id);
		var receipt = new FeePaymentReceipt(Guid.NewGuid())
		{
			StudentId = null, FeeCollectionTransactionId = metadata.Id, ReceiptNumber = "FIN-2026-1",
			TotalPaidAmount = 500, ReceiptKind = FeeReceiptKind.FinalFee,
		};
		receipt.SetFinanceFeeAccount(account.Id);
		var dueId = Guid.NewGuid();
		var allocation = new FeeCollectionDueFeeAllocation(Guid.NewGuid(), account.TenantId, metadata.Id, dueId, 500);
		var student = Guid.NewGuid();
		metadata.LinkStudent(student);
		payment.LinkStudent(student);
		record.LinkStudent(student);
		receipt.LinkStudent(student);
		Assert.Equal(account.Id, metadata.FinanceFeeAccountId);
		Assert.Equal(account.Id, payment.FinanceFeeAccountId);
		Assert.Equal(account.Id, record.FinanceFeeAccountId);
		Assert.Equal(account.Id, receipt.FinanceFeeAccountId);
		Assert.Equal(metadata.Id, receipt.FeeCollectionTransactionId);
		Assert.Equal(metadata.Id, allocation.FeeRecordMetadataId);
		Assert.Equal(dueId, allocation.DueFeeId);
		Assert.Equal("FIN-2026-1", receipt.ReceiptNumber);
		Assert.Equal(500d, receipt.TotalPaidAmount);
		Assert.Equal(500m, payment.Amount);
		Assert.Throws<BusinessException>(() => receipt.LinkStudent(Guid.NewGuid()));
	}

	[Fact]
	public void Applicant_Refund_Uses_Original_Receipt_And_Survives_Student_Link()
	{
		var account = CreateAccount();
		var refund = new FeeRefundRequest(Guid.NewGuid(), account.TenantId, null, account.AcademicYearId,
			"REF-APPLICANT", "Cash", null, "Admission cancelled", financeFeeAccountId: account.Id);
		var receiptId = Guid.NewGuid();
		var policyVersion = Guid.NewGuid();
		refund.PinPolicy(null, Guid.NewGuid(), policyVersion, receiptId, "FIN-2026-1", "{}", "{}", new string('a', 64));
		var line = new FeeRefundLine(Guid.NewGuid(), refund.Id, Guid.NewGuid(), Guid.NewGuid(), receiptId,
			Guid.NewGuid(), "Admission fee", "One-time", 500m, 500m, 500m);
		refund.AddLine(line);
		refund.Submit(Guid.NewGuid());
		refund.Approve(Guid.NewGuid(), [new FeeRefundReservation(Guid.NewGuid(), refund.Id, line.Id,
			line.DueFeeId, line.SourceAllocationId, 500m)]);
		var processor = Guid.NewGuid();
		var settlement = refund.StartProcessing(Guid.NewGuid(), processor, "applicant-refund", "Cash", 500m,
			DateTimeOffset.UtcNow, "CASH-1", "{}");
		refund.CompleteSettlement(settlement.Id, processor);
		var student = Guid.NewGuid();
		refund.LinkStudent(student);
		refund.LinkStudent(student);
		Assert.Equal(student, refund.StudentId);
		Assert.Equal(account.Id, refund.FinanceFeeAccountId);
		Assert.Null(refund.StudentFeeStructureId);
		Assert.Equal(receiptId, refund.OriginalReceiptId);
		Assert.Equal(policyVersion, refund.RefundPolicyVersionId);
		Assert.Equal(500m, refund.ProcessedAmount);
		Assert.Single(refund.Settlements);
		Assert.Equal(FeeRefundStatus.Processed, refund.Status);
		Assert.Throws<BusinessException>(() => refund.LinkStudent(Guid.NewGuid()));
	}

	[Fact]
	public void Pinned_Policies_Are_Read_From_Snapshot_And_Invalid_Evidence_Fails_Closed()
	{
		var account = CreateAccount();
		var policy = Guid.NewGuid();
		var version = Guid.NewGuid();
		account.CaptureAssignment(Guid.NewGuid(), 1,
			$"{{\"policies\":[{{\"family\":\"collection\",\"policyId\":\"{policy}\",\"versionId\":\"{version}\"}}]}}", new string('a', 64));
		Assert.Equal((policy, version), account.GetPolicyReference("collection"));
		Assert.Null(account.GetPolicyReference("refund"));
		Assert.Throws<BusinessException>(() => account.SetDivision(Guid.Empty));
		var division = Guid.NewGuid();
		account.SetDivision(division);
		Assert.Equal(division, account.DivisionId);
		var invalid = CreateAccount();
		invalid.CaptureAssignment(Guid.NewGuid(), 1, "{}", new string('a', 64));
		Assert.Throws<BusinessException>(() => invalid.GetPolicyReference("collection"));
	}

	[Fact]
	public void Only_Unpaid_Unenrolled_Preparation_Can_Be_Replaced()
	{
		var account = CreateAccount();
		account.CaptureAssignment(Guid.NewGuid(), 1, "first", new string('a', 64));
		account.ResetUnpaidPreparation();
		Assert.Null(account.AssignmentSnapshotJson);
		account.CaptureAssignment(Guid.NewGuid(), 2, "second", new string('b', 64));
		account.RecordPaymentEvidence();
		account.RecordPaymentEvidence();
		Assert.True(account.HasPaymentEvidence);
		Assert.Throws<BusinessException>(account.ResetUnpaidPreparation);
		Assert.Equal("second", account.AssignmentSnapshotJson);
		var enrolled = CreateAccount();
		enrolled.CaptureAssignment(Guid.NewGuid(), 1, "enrolled", new string('a', 64));
		enrolled.LinkStudent(Guid.NewGuid(), DateTimeOffset.UtcNow);
		enrolled.BindStudentAssignment(Guid.NewGuid());
		Assert.Throws<BusinessException>(enrolled.ResetUnpaidPreparation);
	}

	[Fact]
	public void Missing_Or_Fake_Payer_Is_Rejected_While_Legacy_Student_Constructors_Work()
	{
		Assert.Throws<BusinessException>(() => FinanceFeePayerIdentity.Validate(null, null));
		Assert.Throws<BusinessException>(() => FinanceFeePayerIdentity.Validate(Guid.Empty, Guid.NewGuid()));
		var student = Guid.NewGuid();
		var legacy = new DueFee(Guid.NewGuid(), null, student, Guid.NewGuid(), MonthEnum.October,
			2026, true, 500m, new DateTime(2026, 10, 12));
		Assert.Equal(student, legacy.StudentId);
		Assert.Null(legacy.FinanceFeeAccountId);
		var accountId = Guid.NewGuid();
		legacy.BindFinanceFeeAccount(accountId);
		legacy.BindFinanceFeeAccount(accountId);
		Assert.Equal(accountId, legacy.FinanceFeeAccountId);
		Assert.Throws<BusinessException>(() => legacy.BindFinanceFeeAccount(Guid.NewGuid()));
		Assert.Equal(500m, legacy.OutstandingAmount);
		var assignment = new StudentFeeStructure(Guid.NewGuid(), null, student, Guid.NewGuid(),
			Guid.NewGuid(), financeFeeAccountId: Guid.NewGuid());
		Assert.Equal(student, assignment.StudentId);
		Assert.NotNull(assignment.FinanceFeeAccountId);
		assignment.BindFinanceFeeAccount(assignment.FinanceFeeAccountId!.Value);
		Assert.Throws<BusinessException>(() => assignment.BindFinanceFeeAccount(Guid.NewGuid()));
	}
}

using ShikshaMitra.Students.Records;
using Shouldly;
using Xunit;
using System;
using System.Text.Json;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;

namespace ShikshaMitra.Students.Records;

public sealed class StudentSchoolExitOutstanding_Tests
{
	[Theory]
	[InlineData("Withdraw", "Students:Records:OutstandingFees", true)]
	[InlineData("Transfer", "Students:Records:OutstandingBooks", true)]
	[InlineData("Graduate", "Students:Records:FinanceClearanceRequired", false)]
	[InlineData("Withdraw", "Students:Records:TransportClearanceRequired", false)]
	[InlineData("Return", "Students:Records:OutstandingFees", false)]
	public void Only_recognized_debt_on_school_exit_is_reviewable(string outcome, string code, bool expected)
	{
		StudentSchoolExitOutstanding.IsReviewable(outcome, code).ShouldBe(expected);
	}
	[Fact]
	public void New_or_changed_owner_evidence_requires_fresh_review()
	{
		StudentSchoolExitOutstanding.Matches("[{\"Code\":\"fees\",\"Evidence\":\"100\"}]", "[{\"Code\":\"fees\",\"Evidence\":\"200\"}]").ShouldBeFalse();
		StudentSchoolExitOutstanding.Matches("same", "same").ShouldBeTrue();
	}
	[Theory]
	[InlineData("{\"Finance\":\"100\"}", "{\"Finance\":\"200\"}")]
	[InlineData("{\"Library\":\"book1\"}", "{\"Library\":\"book1,book2\"}")]
	public void Approved_evidence_cannot_cover_increased_money_or_new_books(string approved, string current)
	{
		var tenant = Guid.NewGuid(); var requester = Guid.NewGuid(); var approver = Guid.NewGuid();
		var rule = new StudentRecordsChangeRule(); var policy = StudentRecordsPolicy.Starter();
		var item = new StudentRecordTransition(Guid.NewGuid(), tenant, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Withdraw", "School exit", DateTime.UtcNow,
			"{}", "{}", null, policy.ToJson(), JsonSerializer.Serialize(rule), requester, 1);
		item.Approve(approver); item.CaptureOutstandingApproval(approved);
		var events = new[] { new StudentRecordTransitionEvent(Guid.NewGuid(), tenant, item.Id, approver, "Approved", 1, "Reviewed"),
			new StudentRecordTransitionEvent(Guid.NewGuid(), tenant, item.Id, approver, "OutstandingApproved", 1, "Recover separately") };
		var row = new StudentRecordTransitionPreviewRowDto { Outcome = "Withdraw", OutstandingEvidence = approved,
			OutstandingItems = [new() { Code = "Students:Records:OutstandingFees" }] };
		StudentSchoolExitOutstanding.RequireApprovedEvidence(item, row, events);
		row.OutstandingEvidence = current;
		Should.Throw<BusinessException>(() => StudentSchoolExitOutstanding.RequireApprovedEvidence(item, row, events)).Code.ShouldBe("Students:Records:OutstandingReviewChanged");
	}
	[Fact]
	public void Missing_stage_reason_and_direct_approval_cannot_bypass_outstanding_review()
	{
		var tenant = Guid.NewGuid(); var requester = Guid.NewGuid(); var approver = Guid.NewGuid();
		var policy = StudentRecordsPolicy.Starter();
		var item = new StudentRecordTransition(Guid.NewGuid(), tenant, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Withdraw", "School exit", DateTime.UtcNow,
			"{}", "{}", null, policy.ToJson(), JsonSerializer.Serialize(policy.Changes["Status"]), requester, 1);
		item.Approve(approver); item.CaptureOutstandingApproval("evidence");
		var row = new StudentRecordTransitionPreviewRowDto { OutstandingEvidence = "evidence", OutstandingItems = [new() { Code = "Students:Records:OutstandingBooks" }] };
		var events = new[] { new StudentRecordTransitionEvent(Guid.NewGuid(), tenant, item.Id, approver, "Approved", 1, "Reviewed"),
			new StudentRecordTransitionEvent(Guid.NewGuid(), tenant, item.Id, approver, "OutstandingApproved", 1, " ") };
		Should.Throw<BusinessException>(() => StudentSchoolExitOutstanding.RequireApprovedEvidence(item, row, events)).Code.ShouldBe("Students:Records:OutstandingReviewChanged");
		Should.Throw<BusinessException>(() => StudentSchoolExitOutstanding.RequireApprovedEvidence(item, row, [])).Code.ShouldBe("Students:Records:OutstandingReviewRequired");
	}
}

using System;
using System.Linq;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.Students.Records;

/// <summary>Only owner-classified monetary debt and unreturned books can be reviewed.</summary>
public static class StudentSchoolExitOutstanding
{
	public static bool IsExit(string outcome) => outcome is "Withdraw" or "Transfer" or "Graduate";
	public static bool IsReviewable(string outcome, string code) => IsExit(outcome) && code is "Students:Records:OutstandingFees" or "Students:Records:OutstandingBooks";
	public static bool Matches(string approved, string current) => string.Equals(approved, current, StringComparison.Ordinal);
	public static void Complete(StudentRecordTransitionPreviewRowDto row)
	{
		row.OutstandingItems = row.Issues.Where(x => IsReviewable(row.Outcome, x.Code)).ToList();
		row.Issues.RemoveAll(x => IsReviewable(row.Outcome, x.Code));
		row.ApprovalRequired = row.OutstandingItems.Count > 0;
		row.Ready = row.Issues.Count == 0;
	}
	public static void RequireApprovedEvidence(StudentRecordTransition item, StudentRecordTransitionPreviewRowDto row, StudentRecordTransitionEvent[] approvals)
	{
		if (row.OutstandingItems.Count == 0) return;
		if (item.ApprovalStageCount == 0) throw new BusinessException("Students:Records:OutstandingReviewRequired");
		for (var stage = 1; stage <= item.ApprovalStageCount; stage++)
		{
			var approval = approvals.SingleOrDefault(x => x.Stage == stage && x.Action == "OutstandingApproved");
			if (approval == null || approval.ActorId == item.RequestedBy) throw new BusinessException("Students:Records:OutstandingReviewRequired");
			var stageApproval = approvals.SingleOrDefault(x => x.Stage == stage && x.Action == "Approved");
			if (stageApproval?.ActorId != approval.ActorId || string.IsNullOrWhiteSpace(approval.Remarks) || !Matches(item.GetOutstandingApproval(stage) ?? "", row.OutstandingEvidence))
				throw new BusinessException("Students:Records:OutstandingReviewChanged");
		}
	}
}


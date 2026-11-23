using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

internal static class AssignmentDueEvidence
{
	public static bool Matches(StudentFeeStructure assignment, StudentFeeAssignmentPreviewDto evidence, IReadOnlyCollection<DueFee> dues)
	{
		var original = evidence.Lines.Select(line => line.DueFeeId).ToHashSet();
		if (original.Contains(null) || original.Count != evidence.Lines.Count ||
			!original.SetEquals(dues.Where(due => !due.GetOptionalServicePeriodId().HasValue).Select(due => (Guid?)due.Id)))
			return false;

		var restarted = dues.Where(due => due.GetOptionalServicePeriodId().HasValue)
			.ToLookup(due => (due.FeeHeadId, due.GetOptionalServicePeriodId()));
		var expectedCount = 0;
		foreach (var head in evidence.Heads.Where(head => head.IsOptional && !head.IsOneTime))
		{
			foreach (var period in assignment.GetOptionalServicePeriods(head.FeeHeadId))
			{
				var rows = restarted[(head.FeeHeadId, period.Id)].ToList();
				if (!MatchesPeriod(period, rows)) return false;
				expectedCount += rows.Count;
			}
		}
		return expectedCount == restarted.Sum(group => group.Count());
	}

	public static bool MatchesPeriod(OptionalFeeServicePeriod period, IReadOnlyCollection<DueFee> rows) =>
		period.DueIds is { } ids && ids.Length == rows.Count &&
		ids.ToHashSet().SetEquals(rows.Select(row => row.Id)) && rows.Sum(row => row.OriginalAmount) == period.AssignedAmount &&
		rows.All(row => row.ProrationPolicyVersionId == period.ProrationPolicyVersionId && row.PaymentTermVersionId == period.PaymentTermVersionId);
}

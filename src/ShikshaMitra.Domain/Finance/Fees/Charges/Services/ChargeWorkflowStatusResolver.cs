using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Finance.Fees.Charges;

public record ChargeWorkflowLine(ChargeStatus Status, decimal OutstandingAmount);

public static class ChargeWorkflowStatusResolver
{
	public static ChargeWorkflowStatus Resolve(IEnumerable<ChargeWorkflowLine> lines)
	{
		var snapshot = lines.ToList();
		if (snapshot.Count == 0 || snapshot.Any(line => line.Status == ChargeStatus.Pending))
			return ChargeWorkflowStatus.Pending;

		var approved = snapshot.Where(line => line.Status == ChargeStatus.Approved).ToList();
		// Voided lines remain immutable correction evidence in the non-collectible inbox.
		if (approved.Count == 0)
			return ChargeWorkflowStatus.Rejected;
		return approved.All(line => line.OutstandingAmount <= 0m)
			? ChargeWorkflowStatus.Completed
			: ChargeWorkflowStatus.InProcess;
	}
}

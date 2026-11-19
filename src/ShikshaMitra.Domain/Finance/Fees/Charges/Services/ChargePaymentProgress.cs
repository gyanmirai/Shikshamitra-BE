using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Finance.Fees.Charges;

/// <summary>Canonical read model over the same amounts shown in the student charge list.</summary>
public record ChargePaymentLine(ChargeStatus Status, decimal Amount, decimal CollectedAmount,
	decimal WaivedAmount, bool HasActiveCollection = false)
{
	public decimal OutstandingAmount => Status == ChargeStatus.Approved
		? Math.Max(0m, Amount - CollectedAmount - WaivedAmount) : 0m;
}

public record ChargePaymentProgress(int PaidStudentCount, int OutstandingStudentCount,
	int WaivedStudentCount, bool CanCancel, ChargeWorkflowStatus WorkflowStatus)
{
	public static ChargePaymentProgress Resolve(IEnumerable<ChargePaymentLine> lines)
	{
		var snapshot = lines.ToList();
		var approved = snapshot.Where(line => line.Status == ChargeStatus.Approved).ToList();
		return new(
			approved.Count(line => line.OutstandingAmount == 0m && line.CollectedAmount > 0m),
			approved.Count(line => line.OutstandingAmount > 0m),
			approved.Count(line => line.WaivedAmount > 0m),
			approved.Count > 0 && approved.All(line => !line.HasActiveCollection),
			ChargeWorkflowStatusResolver.Resolve(snapshot.Select(line =>
				new ChargeWorkflowLine(line.Status, line.OutstandingAmount))));
	}
}

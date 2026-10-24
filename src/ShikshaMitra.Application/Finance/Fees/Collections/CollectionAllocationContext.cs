using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Collections.Dtos;

namespace ShikshaMitra.Finance.Fees.Collections;

/// <summary>One ordered ledger for display, review and server allocation. No fee-head priorities are stored.</summary>
internal static class CollectionAllocationContext
{
	public static FeeCollectFormDto Apply(FeeCollectFormDto form, CollectionPolicy? policy)
	{
		form.ReceiptGrouping = (policy?.ReceiptGrouping ?? CollectionReceiptGrouping.Separate).ToString();
		form.AllocationBasis = (policy?.AllocationBasis ?? CollectionAllocationBasis.Legacy).ToString();
		form.CollectionPolicyId = policy?.Id;
		form.CollectionPolicyName = policy?.PolicyName;
		form.CollectionPolicyVersion = policy?.Version;
		if (policy is null || policy.AllocationBasis == CollectionAllocationBasis.Legacy) return form;

		// Retain the collection screen's chronological ledger; its head view groups by displayed name.
		var lines = form.Ledger.SelectMany(month => month.Items).ToList();
		form.AllocationDueFeeOrder = (policy.AllocationBasis == CollectionAllocationBasis.FeeHead
			? lines.OrderBy(line => line.FeeHeadName, StringComparer.OrdinalIgnoreCase)
			: lines.GroupBy(line => line.InstallmentId?.ToString() ?? $"{line.DueDate:yyyy-MM-dd}|{line.InstallmentLabel}")
				.SelectMany(group => group)).Select(line => line.DueFeeId).ToList();
		var evidence = JsonSerializer.Serialize(new
		{
			form.Student.StudentId, form.Student.FinanceFeeAccountId,
			form.CollectionPolicyId, form.CollectionPolicyVersion, form.AllocationBasis,
			form.AllocationDueFeeOrder,
			Lines = lines.Select(line => new { line.DueFeeId, line.FeeHeadId, line.Due, line.LateFeeAmount, line.Status, line.ReconciliationAmount }),
			Charges = form.Charges.OrderBy(charge => charge.ChargeId).Select(charge => new
			{ charge.ChargeId, charge.SourceType, charge.SourceId, charge.PendingAmount, charge.ReconciliationAmount, charge.Status })
		});
		form.AllocationRevision = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidence)));
		return form;
	}
}

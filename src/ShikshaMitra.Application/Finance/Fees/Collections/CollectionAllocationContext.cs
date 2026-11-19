using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.Settings;

namespace ShikshaMitra.Finance.Fees.Collections;

/// <summary>One ordered ledger for display, review and server allocation. No fee-head priorities are stored.</summary>
internal static class CollectionAllocationContext
{
	public static FeeCollectFormDto Apply(FeeCollectFormDto form, CollectionPolicy? policy, IReadOnlyList<Guid> feeHeadOrder)
	{
		form.ReceiptGrouping = (policy?.ReceiptGrouping ?? CollectionReceiptGrouping.Separate).ToString();
		form.AllocationBasis = (policy?.AllocationBasis ?? CollectionAllocationBasis.Legacy).ToString();
		form.CollectionPolicyId = policy?.Id;
		form.CollectionPolicyName = policy?.PolicyName;
		form.CollectionPolicyVersion = policy?.Version;
		if (policy is null || policy.AllocationBasis == CollectionAllocationBasis.Legacy) return form;

		var lines = form.Ledger.SelectMany(month => month.Items).ToList();
		var rank = FeeStructureHeadOrder.CreateRank(feeHeadOrder);
		form.AllocationDueFeeOrder = (policy.AllocationBasis == CollectionAllocationBasis.FeeHead
			? lines.GroupBy(line => line.FeeHeadId)
				.OrderBy(group => rank.GetValueOrDefault(group.Key, int.MaxValue))
				.ThenBy(group => group.Key)
				.SelectMany(group => group.OrderBy(line => line.DueDate))
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

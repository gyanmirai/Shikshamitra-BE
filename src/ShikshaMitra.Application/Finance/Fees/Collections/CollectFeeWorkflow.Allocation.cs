using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Domain.Finance.Policies;
using Volo.Abp;

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class CollectFeeWorkflow
{
	private async Task<FeeCollectFormDto?> ValidateAutomaticAllocationAsync(CollectFeeInput input, FinanceFeeAccount? account, CollectionPolicy? policy)
	{
		if (policy == null || policy.AllocationBasis == CollectionAllocationBasis.Legacy)
		{
			if (input.AutomaticAllocation != null) throw AllocationChanged();
			return null;
		}
		// Resolve through the same scoped, version-pinned form as the collection screen.
		var form = account != null && !account.StudentFeeStructureId.HasValue
			? await LazyServiceProvider.LazyGetRequiredService<AdmissionFeeCollectionAppService>().GetCollectFormAsync(account.ApplicationId)
			: await LazyServiceProvider.LazyGetRequiredService<StudentFeeDetailsQueryService>()
				.GetCollectFormAsync(input.StudentId!.Value, input.AcademicYearId);
		if (form.AllocationBasis == "Legacy")
		{
			if (input.AutomaticAllocation != null) throw AllocationChanged();
			return null;
		}
		var selection = input.AutomaticAllocation;
		if (selection == null || selection.Revision != form.AllocationRevision ||
			selection.DueFeeIds == null || selection.ChargeIds == null) throw AllocationChanged();
		if (selection.DueFeeIds.Count > 200 || selection.ChargeIds.Count > 20 ||
			selection.DueFeeIds.Distinct().Count() != selection.DueFeeIds.Count ||
			selection.ChargeIds.Distinct().Count() != selection.ChargeIds.Count)
			throw AllocationChanged();

		var selectedDues = selection.DueFeeIds.ToHashSet();
		var lines = form.Ledger.SelectMany(month => month.Items).ToDictionary(line => line.DueFeeId);
		var orderedIds = form.AllocationDueFeeOrder.Where(selectedDues.Contains).ToList();
		if (orderedIds.Count != selectedDues.Count) throw AllocationChanged();
		var targets = new List<CollectionAllocationTarget>();
		foreach (var id in orderedIds)
		{
			var line = lines[id];
			if (line.Status is FeeCollectionStatus.InReconciliation or FeeCollectionStatus.Waived ||
				line.ReconciliationAmount > 0d || line.Due <= 0d && line.LateFeeAmount <= 0d)
				throw AllocationChanged();
			// Retain the existing principal ceiling contract; do not round the entered payment.
			targets.Add(new(id, CollectionAllocationComponent.Principal, Math.Ceiling((decimal)line.Due)));
			if (line.LateFeeAmount > 0d) targets.Add(new(id, CollectionAllocationComponent.LateFee, (decimal)line.LateFeeAmount));
		}
		var charges = form.Charges.ToDictionary(charge => charge.ChargeId);
		foreach (var id in selection.ChargeIds)
		{
			if (!charges.TryGetValue(id, out var charge) || charge.PendingAmount <= 0d ||
				charge.ReconciliationAmount > 0m || charge.Status == FeeCollectionStatus.InReconciliation)
				throw AllocationChanged();
			targets.Add(new(charge.SourceId, charge.SourceType == "LateFee"
				? CollectionAllocationComponent.LateFee : CollectionAllocationComponent.Charge, (decimal)charge.PendingAmount));
		}
		var expected = SequentialCollectionAllocation.Allocate(targets, input.Payments.Sum(payment => (decimal)payment.Amount));
		var actual = input.DueFeePayments.Select(line => new CollectionAllocatedAmount(line.DueFeeId, CollectionAllocationComponent.Principal, (decimal)line.Amount))
			.Concat(input.LateFeePayments.Select(line => new CollectionAllocatedAmount(line.DueFeeId, CollectionAllocationComponent.LateFee, (decimal)line.Amount)))
			.Concat(input.ChargePayments.Select(line => new CollectionAllocatedAmount(line.ChargeId, CollectionAllocationComponent.Charge, (decimal)line.Amount))).ToList();
		if (expected.Count != actual.Count || actual.DistinctBy(line => (line.Id, line.Component)).Count() != actual.Count ||
			expected.Any(line => !actual.Contains(line)) || input.DueFeePayments.Any(line => !lines.TryGetValue(line.DueFeeId, out var due) || due.FeeHeadId != line.FeeHeadId))
			throw AllocationChanged();
		return form;
	}

	private static BusinessException AllocationChanged() => new(FeeCollectionErrorCodes.AllocationChanged);
}

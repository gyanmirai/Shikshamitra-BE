using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms.Dtos;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureAppService
{
	private const string UnchangedRevisionFeeHeadsMessage =
		"Change at least one fee head or payment schedule from the published structure before saving this revision.";

	private async Task EnsureRevisionFeeHeadsChangedAsync(FeeStructure entity, StandardFeeStructureRequest candidate)
	{
		if (entity.PreviousRevisionId is not { } previousId) return;

		using var untrackedParent = feeStructureRepository.DisableTracking();
		var previous = ToRequest(await feeStructureRepository.GetAsync(previousId));
		var previousHeads = previous.FeeHeads.OrderBy(line => line.FeeHeadId);
		var candidateHeads = candidate.FeeHeads.OrderBy(line => line.FeeHeadId);
		if (previous.ProrationPolicyVersionId == candidate.ProrationPolicyVersionId &&
			previous.FeeHeads.Count == candidate.FeeHeads.Count &&
			previousHeads.Zip(candidateHeads, SameFeeHeadConfiguration).All(same => same))
		{
			throw new BusinessException(FeeStructureErrorCodes.UnchangedRevisionFeeHeads)
				.WithData("Errors", new[] { UnchangedRevisionFeeHeadsMessage });
		}
	}

	private static bool SameFeeHeadConfiguration(FeeHeadAssignmentRequest previous, FeeHeadAssignmentRequest candidate) =>
		previous.FeeHeadId == candidate.FeeHeadId &&
		WholeRupeeAmounts.RoundUp(previous.Amount) == WholeRupeeAmounts.RoundUp(candidate.Amount) &&
		previous.IsVariable == candidate.IsVariable &&
		previous.MonthlyRate == candidate.MonthlyRate &&
		previous.IsOptional == candidate.IsOptional &&
		previous.IsOneTime == candidate.IsOneTime &&
		(!candidate.IsOneTime || previous.PayableMonth == candidate.PayableMonth && previous.PayableYear == candidate.PayableYear) &&
		previous.PaymentTermId == candidate.PaymentTermId &&
		previous.PaymentTermVersionId == candidate.PaymentTermVersionId &&
		previous.ScheduleMode == candidate.ScheduleMode &&
		(candidate.ScheduleMode != ScheduleMode.Customized ||
			previous.InstallmentOverrides.OrderBy(row => row.SourcePaymentTermInstallmentId).Select(OverrideConfiguration)
				.SequenceEqual(candidate.InstallmentOverrides.OrderBy(row => row.SourcePaymentTermInstallmentId).Select(OverrideConfiguration)));

	private static (Guid SourceId, int Sequence, string Name, DateTime? Start,
		DateTime? End, DateTime? Demand, DateTime? Due, decimal? Percentage)
		OverrideConfiguration(ScheduleOverrideDto row) =>
		(row.SourcePaymentTermInstallmentId, row.Sequence, row.Name, row.CoverageStartDate,
			row.CoverageEndDate, row.DemandDate, row.DueDate, row.AllocationPercentage);
}

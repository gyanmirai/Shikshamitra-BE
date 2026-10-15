using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

public partial class StudentFeeStructureAppService
{
	private Task<PreparedAssignment> PrepareAsync(AssignStudentFeeStructureDto input) => PrepareForContextAsync(input, null);

	private async Task<PreparedAssignment> PrepareForContextAsync(AssignStudentFeeStructureDto input, AdmissionAssignmentContext? admission)
	{
		ValidateInput(input, admission != null);
		var enrollment = admission == null ? await RequireEnrollmentAsync(input.StudentId, input.AcademicYearId, true) : null;
		var studentId = admission?.StudentId ?? enrollment?.StudentId;
		var standardId = admission?.StandardId ?? enrollment?.StandardId;
		// These are calculation inputs, not assignment writes. Tracking their graphs lets
		// ABP propagate student/due changes to shared principals and rotate the revisions
		// used by every other student's preview in the same bulk operation.
		using var structureTracking = feeStructureRepository.DisableTracking();
		using var detailTracking = feeStructureDetailRepository.DisableTracking();
		using var metadataTracking = feeStructureMetadataRepository.DisableTracking();
		using var yearTracking = academicYearRepository.DisableTracking();
		var structure = await feeStructureRepository.GetAsync(input.StandardFeeStructureId);
		var metadata = await feeStructureMetadataRepository.GetAsync(structure.FeeStructureMetadataId);
		if (!structure.IsLocked || structure.StandardId != standardId || metadata.AcademicYearId != input.AcademicYearId ||
			(admission != null && (metadata.AcademicBoardId != admission.BoardId || metadata.AcademicMediumId != admission.MediumId || structure.RevisionNumber != admission.StructureRevision)))
			throw new BusinessException(AssignmentErrorCodes.StructureMismatch);
		var year = await academicYearRepository.GetAsync(input.AcademicYearId);
		var details = (await feeStructureDetailRepository.GetListAsync(x => x.FeeStructureId == structure.Id)).OrderBy(x => x.Id).ToList();
		if (details.Count == 0 || details.Select(x => x.FeeHeadId).Distinct().Count() != details.Count ||
			input.SelectedOptionalFeeHeadIds.Any(id => !details.Any(d => d.FeeHeadId == id && d.IsOptional)) ||
			input.FeeHeadDetails.Any(x => !details.Any(d => d.FeeHeadId == x.FeeHeadId && !d.IsVariable && (!d.IsOptional || input.SelectedOptionalFeeHeadIds.Contains(d.FeeHeadId)))) ||
			input.ServicePeriods.Any(x => !details.Any(d => d.FeeHeadId == x.FeeHeadId && (!d.IsOptional || input.SelectedOptionalFeeHeadIds.Contains(d.FeeHeadId)))))
			throw new BusinessException(AssignmentErrorCodes.InvalidInput);
		if (input.VariableFeeHeads.Any(x => !details.Any(d => d.FeeHeadId == x.FeeHeadId && d.IsVariable &&
                (!d.IsOptional || input.SelectedOptionalFeeHeadIds.Contains(d.FeeHeadId)))))
            throw new BusinessException(AssignmentErrorCodes.InvalidInput);
        foreach (var detail in details.Where(d => structure.FeeStructureDetails.All(x => x.Id != d.Id))) structure.AddDetail(detail);
		var selectedPolicies = await ResolveAssignedPoliciesAsync(structure, year.Id, input.Policies, input.PolicyOverrideReason);
		var collection = selectedPolicies["collection"];
		var proration = selectedPolicies["proration"];
		var refund = selectedPolicies["refund"];
		var concession = selectedPolicies["concession"];
		ProrationPolicySnapshot? prorationRules;
		CollectionPolicySnapshot collectionRules;
		RefundPolicySnapshot? refundRules;
		try
		{
			collectionRules = CollectionPolicySnapshot.Deserialize(collection!.SnapshotJson);
			prorationRules = proration == null ? null : JsonSerializer.Deserialize<ProrationPolicySnapshot>(proration.SnapshotJson);
			refundRules = refund == null ? null : JsonSerializer.Deserialize<RefundPolicySnapshot>(refund.SnapshotJson);
			if (collectionRules == null || (proration != null && prorationRules == null) || (refund != null && refundRules == null))
				throw new JsonException();
		}
		catch (JsonException) { throw new BusinessException(AssignmentErrorCodes.ConfigurationInvalid); }
		if (collectionRules.LateFeeApplyOnFeeHeads == null || collectionRules.FeeHeadOverrides == null ||
			(prorationRules != null && (prorationRules.PolicyId != proration!.PolicyId || prorationRules.AcademicYearId != year.Id || prorationRules.Rules == null || prorationRules.Rules.Any(r => r == null))) ||
			(refundRules != null && (refundRules.PolicyId != refund!.PolicyId || refundRules.AcademicYearId != year.Id || refundRules.ApplicableFeeHeadIds == null)))
			throw new BusinessException(AssignmentErrorCodes.ConfigurationInvalid);
		var headIds = details.Select(d => d.FeeHeadId).ToList();
		var headRecords = (await feeHeadRepository.GetListAsync(h => headIds.Contains(h.Id))).ToDictionary(h => h.Id);
		var names = headRecords.ToDictionary(h => h.Key, h => h.Value.Name);
		var preview = new StudentFeeAssignmentPreviewDto {
			StudentId = studentId, AcademicYearId = year.Id, EnrollmentId = enrollment?.Id,
			StructureId = structure.Id, StructureName = structure.Name, RevisionNumber = structure.RevisionNumber,
			ChargeStartDate = DateTime.SpecifyKind(year.StartDate.Date, DateTimeKind.Utc), ChargeEndDate = DateTime.SpecifyKind(year.EndDate.Date, DateTimeKind.Utc),
			OverrideReason = input.OverrideReason?.Trim(),
			PolicyOverrideReason = input.PolicyOverrideReason?.Trim(),
			ProrationPolicyId = proration?.PolicyId, ProrationPolicyVersionId = proration?.Id
		};
		var optionalDetails = new List<CustomOptionalFeeHeadDetail>();
		foreach (var detail in details)
		{
			var included = !detail.IsOptional || input.SelectedOptionalFeeHeadIds.Contains(detail.FeeHeadId);
			var head = new AssignmentHeadDto { FeeHeadId = detail.FeeHeadId, FeeHeadName = names.GetValueOrDefault(detail.FeeHeadId, "Fee head"),
				IsOptional = detail.IsOptional, IsVariable = detail.IsVariable, IsOneTime = detail.IsOneTime, Included = included, ConfiguredAmount = detail.Amount,
				EffectiveAmount = included ? detail.Amount : null, AmountBasis = detail.IsVariable ? "CoverageTotal" : detail.IsOneTime ? "OneTime" : "AnnualTotal",
				PaymentTermId = detail.PaymentTermId, PaymentTermVersionId = detail.PaymentTermVersionId };
			preview.Heads.Add(head);
			if (!included) continue;
			// New assignments must preserve an explicit published term, including one-time heads.
			if (!detail.PaymentTermId.HasValue || !detail.PaymentTermVersionId.HasValue)
				throw new BusinessException(AssignmentErrorCodes.ConfigurationInvalid).WithData("Reason", "Publish a payment term for every included fee head.");
			var termVersion = await paymentTermVersionRepository.GetAsync(detail.PaymentTermVersionId.Value);
			var term = await paymentTermRepository.GetAsync(detail.PaymentTermId.Value);
			if (termVersion.PaymentTermId != term.Id || term.AcademicYearId != year.Id)
				throw new BusinessException(AssignmentErrorCodes.ConfigurationInvalid);
			head.PaymentTermName = term.Name;
			head.PaymentTermVersionNumber = termVersion.VersionNumber;
			if (detail.IsVariable)
            {
                var variable = input.VariableFeeHeads.SingleOrDefault(x => x.FeeHeadId == detail.FeeHeadId);
                if (variable?.Amount == null)
                    throw new BusinessException(AssignmentErrorCodes.VariableAmountRequired).WithData("FeeHeadId", detail.FeeHeadId);
                head.EffectiveAmount = variable.Amount;
                head.ZeroAmountReason = variable.ZeroAmountReason?.Trim();
            }
            var amountInput = input.FeeHeadDetails.FirstOrDefault(x => x.FeeHeadId == detail.FeeHeadId);
			if (amountInput != null)
			{
				if (amountInput.PayableMonth.HasValue || amountInput.StartPayableMonth.HasValue || amountInput.EndPayableMonth.HasValue ||
					amountInput.ChargeStartDate.HasValue || amountInput.ChargeEndDate.HasValue)
					throw new BusinessException(AssignmentErrorCodes.InvalidInput);
				if (amountInput.Amount != detail.Amount)
					throw new BusinessException(AssignmentErrorCodes.InvalidInput)
						.WithData("FeeHeadId", detail.FeeHeadId);
			}
			var requested = input.ServicePeriods.FirstOrDefault(x => x.FeeHeadId == detail.FeeHeadId)
				?? new AssignmentServicePeriodDto { FeeHeadId = detail.FeeHeadId, Source = input.ChargePeriodSource,
					StartDate = input.ChargeStartDate, EndDate = input.ChargeEndDate, SourceReferenceId = input.ChargePeriodSourceReferenceId, Reason = input.ChargePeriodOverrideReason };
			if (requested.Source == "TransportService" && (detail.IsVariable || headRecords[detail.FeeHeadId].Module != ShikshaMitra.Finance.Fees.Settings.FeeHeadModule.Transport))
				throw new BusinessException(AssignmentErrorCodes.InvalidInput);
			if (admission != null && requested.Source != "AcademicYear") throw new BusinessException(AssignmentErrorCodes.SourceUnavailable);
			var fact = await ResolveServiceAsync(input.StudentId, requested, year);
			head.ServicePeriod = fact.Period;
			head.Verification = detail.IsVariable ? "Student amount entered during assignment; " + fact.Verification : fact.Verification;
			head.SourceRevision = fact.Revision;
			optionalDetails.Add(new CustomOptionalFeeHeadDetail { FeeHeadId = detail.FeeHeadId, Amount = head.EffectiveAmount ?? throw new BusinessException(AssignmentErrorCodes.VariableAmountRequired),
				ChargeStartDate = fact.Period.StartDate, ChargeEndDate = fact.Period.EndDate });
		}
		var dues = (await dueFeeManager.GenerateForStudentAsync(studentId, structure, year,
			input.SelectedOptionalFeeHeadIds, optionalDetails, prorationRules, proration?.Id, year.StartDate, year.EndDate,
			financeFeeAccountId: admission?.AccountId)).OrderBy(x => x.FeeHeadId).ThenBy(x => x.InstallmentSequence).ToList();
		foreach (var due in dues)
		{
			var head = preview.Heads.Single(x => x.FeeHeadId == due.FeeHeadId);
			head.Obligation += due.OriginalAmount;
			preview.Lines.Add(new StudentFeeAssignmentPreviewLineDto {
				FeeHeadId = due.FeeHeadId, FeeHeadName = head.FeeHeadName, InstallmentLabel = due.GetDisplayInstallmentLabel(),
				Sequence = due.InstallmentSequence ?? 1, PaymentTermVersionId = due.PaymentTermVersionId,
				DueDate = due.DueDate, DemandDate = due.DemandDate, CoverageStartDate = due.CoverageStartDate, CoverageEndDate = due.CoverageEndDate,
				AllocationPercentage = due.AllocationPercentage ?? 100m,
				ScheduledAmount = due.ScheduledAmount, ProratedAmount = due.OriginalAmount, OutstandingAmount = due.OutstandingAmount,
				ReductionAmount = due.ScheduledAmount - due.OriginalAmount, Factor = due.ProrationFactor,
				RuleType = due.ProrationRuleType?.ToString(), RoundingMode = due.ProrationRoundingMode?.ToString(), Explanation = due.ProrationExplanation });
		}
		foreach (var version in new[] { collection, proration, refund, concession }.Where(x => x != null))
		{
			var policy = new AssignmentPolicyDto { Family = version!.PolicyType, PolicyId = version.PolicyId, VersionId = version.Id,
				Version = version.VersionNumber, Name = version.PolicyName };
			policy.Source = StructurePolicyVersions(structure)[version.PolicyType] == version.Id ? "FeeStructure" : "StudentOverride";
			foreach (var head in preview.Heads)
				policy.HeadTreatment[head.FeeHeadId] = !head.Included ? "Not included" : version.PolicyType switch {
					"collection" => collectionRules.EnableLateFee && collectionRules.LateFeeApplyOnFeeHeads.Contains(head.FeeHeadId)
						? "Configured for late-fee evaluation; overrides, grace and exemptions checked on collection. No penalty on assignment."
						: "No base late-fee rule; collection and waiver conditions are checked on collection.",
					"proration" => head.IsOneTime ? "One-time head: full charge" : prorationRules?.Rules.FirstOrDefault(x => x.FeeHeadId == head.FeeHeadId)?.RuleType.ToString() ?? "No matching rule: full charge",
					"concession" => "Selected policy for a separate concession request; no concession applied on assignment.",
					_ => refundRules?.FeeHeadScope == RefundFeeHeadScope.AllFeeHeads || refundRules?.ApplicableFeeHeadIds.Contains(head.FeeHeadId) == true
						? "Eligible head for a separate refund evaluation; no refund created." : "Excluded from refund policy"
				};
			preview.Policies.Add(policy);
		}
		preview.ScheduledTotal = preview.Lines.Sum(x => x.ScheduledAmount);
		preview.ProratedTotal = preview.Lines.Sum(x => x.ProratedAmount);
		preview.ReductionTotal = preview.ScheduledTotal - preview.ProratedTotal;
		if (preview.Lines.Any(x => x.DueDate.Date < Clock.Now.Date)) preview.Warnings.Add("Some configured due dates are in the past. Assignment preserves those dates; it does not assess a late fee.");
		var fingerprint = Hash(new { Preview = preview, structure.ConcurrencyStamp, Enrollment = enrollment?.ConcurrencyStamp ?? admission?.SourceRevision,
			Snapshots = new[] { collection?.SnapshotJson, proration?.SnapshotJson, refund?.SnapshotJson, concession?.SnapshotJson } });
		return new PreparedAssignment(structure, preview, dues, fingerprint);
	}

	private async Task<FinancePolicyVersion?> LoadVersionAsync(string family, Guid? id, Guid? versionId, Guid year, bool required)
	{
		if (!id.HasValue && !versionId.HasValue && !required) return null;
		if (!id.HasValue || !versionId.HasValue) throw new BusinessException(AssignmentErrorCodes.ConfigurationInvalid);
		var version = await financePolicyVersionRepository.FindAsync(versionId.Value);
		if (version == null || version.PolicyId != id || version.PolicyType != family || version.AcademicYearId != year)
			throw new BusinessException(AssignmentErrorCodes.ConfigurationInvalid);
		return version;
	}
}

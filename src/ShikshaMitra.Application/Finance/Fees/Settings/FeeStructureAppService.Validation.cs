using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms.Dtos;
using Volo.Abp;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureAppService
{
	private static readonly JsonSerializerOptions SnapshotOptions = new() { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };

	private async Task<FeeStructurePreviewDto> ValidateCandidateAsync(Guid metadataId, Guid standardId,
		StandardFeeStructureRequest candidate, FeeStructure? current, bool publishing)
	{
		var scope = await EnsureStandardScopeAsync(standardId);
		var metadata = await GetMetadataReferenceAsync(metadataId);
		if (scope.AcademicYearId != metadata.AcademicYearId || scope.EducationBoardId != metadata.AcademicBoardId || scope.EducationMediumId != metadata.AcademicMediumId)
			throw new BusinessException(FeeStructureErrorCodes.ScopeMismatch);
		candidate.ReceiptBookId = null; // Retired input; historical aggregate storage is left intact.
		var result = new FeeStructurePreviewDto { Candidate = candidate };
		var shape = new StandardFeeStructureRequestValidator().Validate(candidate);
		result.Errors.AddRange(shape.Errors.Select(x => x.ErrorMessage));
		if (!shape.IsValid) return result;
		if (publishing && candidate.FeeHeads.Count == 0) result.Errors.Add("Add at least one Fee Head before publishing.");
		if (candidate.FeeHeads.Select(x => x.FeeHeadId).Distinct().Count() != candidate.FeeHeads.Count)
			result.Errors.Add("A Fee Head can appear only once in a structure.");
		var ids = candidate.FeeHeads.Select(x => x.FeeHeadId).Distinct().ToList();
		var heads = await feeHeadRepository.GetListAsync(x => ids.Contains(x.Id));
		if (heads.Count != ids.Count || heads.Any(x => x.AcademicYearId != metadata.AcademicYearId || !x.IsActive))
			result.Errors.Add("Select active Fee Heads from this academic year.");
		foreach (var line in candidate.FeeHeads.Where(x => x.Id.HasValue))
			if (current == null || !current.FeeStructureDetails.Any(x => x.Id == line.Id && x.FeeHeadId == line.FeeHeadId))
				result.Errors.Add("A Fee Head line no longer belongs to this draft. Refresh the structure.");
		var board = await GetBoardCalendarAsync(metadata.AcademicBoardId!.Value);
		var year = await academicYearRepository.GetAsync(metadata.AcademicYearId!.Value);
		foreach (var line in candidate.FeeHeads)
		{
			var head = heads.SingleOrDefault(x => x.Id == line.FeeHeadId);
			if (head == null) continue;
			var coverage = new FeeStructureCoverageDto { FeeHeadId = head.Id, Name = head.Name };
			result.Coverage.Add(coverage);
			try
			{
				var map = await accountingManager.GetValidMappingAsync(year.Id, head.IncomeCategoryId);
				coverage.Accounting = "Ready";
			}
			catch (BusinessException) { coverage.Accounting = "Mapping needs attention"; if (publishing) result.Errors.Add($"{head.Name}: configure an active Income Category and Income Account Head."); }
			if (!line.PaymentTermId.HasValue)
			{
				if (line.PaymentTermVersionId.HasValue || line.InstallmentOverrides.Count > 0 || line.ScheduleMode != ScheduleMode.Inherited)
					result.Errors.Add($"{head.Name}: select a complete Payment Term/version before adding overrides.");
				if (publishing) result.Errors.Add($"{head.Name}: select a published Payment Term. The amount is the total for its service coverage.");
				continue;
			}
			try
			{
				var term = await paymentTermRepository.FindAsync(line.PaymentTermId.Value);
				var version = term?.Versions.SingleOrDefault(x => x.Id == line.PaymentTermVersionId);
				if (term == null || term.Status != PaymentTermStatus.Active || term.AcademicYearId != year.Id || term.BoardId != board.Id ||
					version == null || version.Status is not (PaymentTermVersionStatus.Published or PaymentTermVersionStatus.Superseded))
					throw new BusinessException(FeeStructureErrorCodes.VersionMismatch);
				var previousLine = current?.FeeStructureDetails.SingleOrDefault(x => x.Id == line.Id);
				if (version.Status != PaymentTermVersionStatus.Published &&
					(previousLine?.PaymentTermId != line.PaymentTermId || previousLine?.PaymentTermVersionId != line.PaymentTermVersionId))
					throw new BusinessException(FeeStructureErrorCodes.VersionMismatch);
				if (line.ScheduleMode == ScheduleMode.Inherited && line.InstallmentOverrides.Count > 0 ||
					line.ScheduleMode == ScheduleMode.Customized && line.InstallmentOverrides.Count == 0 ||
					line.InstallmentOverrides.Select(x => x.SourcePaymentTermInstallmentId).Distinct().Count() != line.InstallmentOverrides.Count)
					throw new BusinessException(FeeStructureErrorCodes.InvalidConfiguration);
				foreach (var change in line.InstallmentOverrides)
				{
					var source = version.Installments.SingleOrDefault(x => x.Id == change.SourcePaymentTermInstallmentId);
					if (source == null || source.Sequence != change.Sequence || source.Name != change.Name)
						throw new BusinessException(FeeStructureErrorCodes.VersionMismatch);
				}
				var detail = new FeeStructureDetail(GuidGenerator.Create(), CurrentTenant.Id, current?.Id ?? Guid.NewGuid(), line.FeeHeadId,
					line.Amount, line.IsOneTime, line.IsOptional, line.PayableMonth, line.PayableYear);
				AttachSchedule(detail, line);
				var resolved = await scheduleResolver.ResolveAsync(detail);
				var installments = resolved.Installments.Select(x => new CreatePaymentTermInstallmentDto
				{
					Sequence = x.Sequence, Name = x.Name, CoverageStartDate = x.CoverageStartDate, CoverageEndDate = x.CoverageEndDate,
					DemandDate = x.DemandDate, DueDate = x.DueDate, AllocationPercentage = x.AllocationPercentage
				}).ToList();
				PaymentTermInstallmentValidator.PrepareAndValidate(version.DemandGenerationRule, version.DemandOffsetDays, installments, board.StartDate, board.EndDate);
				if (installments.Sum(x => x.AllocationPercentage) != 100m || resolved.Installments.Any(x => x.CalculatedAmount < 0))
					throw new BusinessException(PaymentTermErrorCodes.PercentageTotalNot100);
				result.Schedules.Add(new ResolvedFeeHeadScheduleDto
				{
					FeeHeadId = head.Id, FeeHeadName = head.Name, AnnualAmount = line.Amount, PaymentTermId = term.Id, PaymentTermName = term.Name,
					PaymentTermVersionId = version.Id, VersionNumber = version.VersionNumber, ScheduleMode = line.ScheduleMode, IsValid = true,
					Installments = resolved.Installments.Select(x => new ResolvedInstallmentDto
					{
						PaymentTermInstallmentId = x.PaymentTermInstallmentId,
						Sequence = x.Sequence, Name = x.Name, CoverageStartDate = x.CoverageStartDate, CoverageEndDate = x.CoverageEndDate,
						DemandDate = x.DemandDate, DueDate = x.DueDate, AllocationPercentage = x.AllocationPercentage, CalculatedAmount = x.CalculatedAmount
					}).ToList()
				});
			}
			catch (BusinessException error) { result.Errors.Add($"{head.Name}: the Payment Term or customized schedule is invalid ({error.Code})."); }
		}

		var availableVersions = await ActivePolicyVersionsAsync(year.Id);
		async Task<FinancePolicyVersion?> Resolve(string family, Guid? id, Guid? selected, Guid? previousId, Guid? previousVersion, bool required)
		{
			if (!id.HasValue)
			{
				if (selected.HasValue) result.Errors.Add($"{family}: a version requires its policy.");
				if (required && publishing) result.Errors.Add($"Select an active {family} policy.");
				return null;
			}
			// Old callers may omit the assertion; an existing attachment must retain its exact version.
			var effective = selected ?? (id == previousId ? previousVersion : null);
			var versions = availableVersions.Where(x => x.PolicyType == family && x.PolicyId == id).OrderByDescending(x => x.VersionNumber).ToList();
			var version = effective.HasValue ? versions.SingleOrDefault(x => x.Id == effective) : versions.FirstOrDefault();
			if (version != null && (id != previousId || version.Id != previousVersion))
			{
				var currentNumber = family switch
				{
					"collection" => (await collectionPolicyRepository.GetAsync(id.Value)).Version,
					"concession" => (await concessionPolicyRepository.GetAsync(id.Value)).Version,
					"proration" => (await prorationPolicyRepository.GetAsync(id.Value)).Version,
					"refund" => (await refundPolicyRepository.GetAsync(id.Value)).Version,
					_ => 0
				};
				if (version.VersionNumber != currentNumber)
					result.Errors.Add($"{family}: the selected snapshot is not the active master's current version. Refresh this policy and explicitly select its current version.");
			}
			if (version == null) result.Errors.Add($"{family}: selected policy/version is unavailable or belongs to another academic year. Select an active version.");
			return version;
		}
		var collection = await Resolve("collection", candidate.CollectionPolicyId, candidate.CollectionPolicyVersionId, current?.CollectionPolicyId, current?.CollectionPolicyVersionId, true);
		// Legacy attachments remain validated; concession setup is no longer required for publication.
		var concession = await Resolve("concession", candidate.ConcessionPolicyId, candidate.ConcessionPolicyVersionId, current?.ConcessionPolicyId, current?.ConcessionPolicyVersionId, false);
		var refund = await Resolve("refund", candidate.RefundPolicyId, candidate.RefundPolicyVersionId, current?.RefundPolicyId, current?.RefundPolicyVersionId, false);
		var proration = await Resolve("proration", candidate.ProrationPolicyId, candidate.ProrationPolicyVersionId, current?.ProrationPolicyId, current?.ProrationPolicyVersionId, false);
		candidate.CollectionPolicyVersionId = collection?.Id;
		candidate.ConcessionPolicyVersionId = concession?.Id;
		candidate.RefundPolicyVersionId = refund?.Id;
		candidate.ProrationPolicyVersionId = proration?.Id;
		foreach (var version in new[] { collection, concession, refund, proration }.Where(x => x != null))
			result.PolicyLabels[version!.PolicyType] = $"{version.PolicyName} · v{version.VersionNumber}";
		try
		{
			var cp = collection == null ? null : CollectionPolicySnapshot.Deserialize(collection.SnapshotJson);
			var cn = concession == null ? null : JsonSerializer.Deserialize<ConcessionPolicySnapshot>(concession.SnapshotJson, SnapshotOptions);
			var rp = refund == null ? null : JsonSerializer.Deserialize<RefundPolicySnapshot>(refund.SnapshotJson, SnapshotOptions);
			var pp = proration == null ? null : JsonSerializer.Deserialize<ProrationPolicySnapshot>(proration.SnapshotJson, SnapshotOptions);
			if (collection != null && (cp == null || string.IsNullOrWhiteSpace(cp.PolicyCode) || cp.LateFeeApplyOnFeeHeads == null) ||
				concession != null && (cn == null || cn.PolicyId != concession.PolicyId || cn.AcademicYearId != year.Id) ||
				refund != null && (rp == null || rp.PolicyId != refund.PolicyId || rp.AcademicYearId != year.Id || rp.ApplicableFeeHeadIds == null) ||
				proration != null && (pp == null || pp.PolicyId != proration.PolicyId || pp.AcademicYearId != year.Id || pp.Rules == null || pp.Rules.Select(x => x.FeeHeadId).Distinct().Count() != pp.Rules.Count))
				throw new JsonException("Invalid policy snapshot identity or rules.");
			var eligible = heads.Where(x => x.IsConcessionApplied).Select(x => x.Id).ToHashSet();
			if (cn != null && candidate.ConcessionFeeHeadScope == ConcessionFeeHeadScope.SelectedFeeHeads &&
				(candidate.ConcessionApplicableFeeHeadIds.Count == 0 || candidate.ConcessionApplicableFeeHeadIds.Any(x => !eligible.Contains(x))))
				result.Errors.Add("Concession eligibility must contain only concession-enabled Fee Heads in this structure.");
			if (cn != null && publishing && eligible.Count == 0) result.Errors.Add("The Concession Policy has no eligible Fee Heads in this structure.");
			if (cn == null)
			{
				candidate.ConcessionFeeHeadScope = ConcessionFeeHeadScope.EntireFeeStructure;
				candidate.ConcessionApplicableFeeHeadIds = [];
			}
			else if (candidate.ConcessionFeeHeadScope == ConcessionFeeHeadScope.EntireFeeStructure)
				candidate.ConcessionApplicableFeeHeadIds = eligible.Order().ToList();
			if (rp is { FeeHeadScope: RefundFeeHeadScope.SelectedFeeHeads } && !rp.ApplicableFeeHeadIds.Any(ids.Contains))
				(publishing ? result.Errors : result.Warnings).Add("The Refund Policy has no matching Fee Heads. Choose a compatible policy or remove the attachment before publishing.");
			foreach (var row in result.Coverage)
			{
				row.Collection = cp is { EnableLateFee: true } && cp.LateFeeApplyOnFeeHeads.Contains(row.FeeHeadId) ? "Late fee applies" : "No late fee";
				if (cp is { EnableWaiver: true, EnableLateFee: true } && cp.LateFeeApplyOnFeeHeads.Contains(row.FeeHeadId)) row.Collection += "; waiver eligible";
				row.Concession = cn != null && candidate.ConcessionApplicableFeeHeadIds.Contains(row.FeeHeadId) ? "Eligible on approval" : "Not eligible";
				row.Refund = rp == null ? "No refund policy attached" : rp.FeeHeadScope != RefundFeeHeadScope.SelectedFeeHeads || rp.ApplicableFeeHeadIds.Contains(row.FeeHeadId) ? "Policy eligible" : "Excluded";
				var rule = pp?.Rules.SingleOrDefault(x => x.FeeHeadId == row.FeeHeadId);
				row.Proration = rule?.RuleType.ToString() ?? "Full charge";
				if (rule != null && rule.RuleType != ProrationRuleType.FullCharge && candidate.FeeHeads.Any(x => x.FeeHeadId == row.FeeHeadId && x.IsOneTime))
					result.Errors.Add($"{row.Name}: a one-time fee cannot use a recurring proration rule.");
			}
			var unused = (cp?.LateFeeApplyOnFeeHeads ?? [])
				.Concat(rp?.ApplicableFeeHeadIds ?? []).Concat(pp?.Rules.Select(x => x.FeeHeadId) ?? []).Distinct().Except(ids).Count();
			if (unused > 0) result.Warnings.Add($"{unused} policy Fee Head(s) are absent from this structure and will not create charges.");
			if (cp is { EnableLateFee: true } && !cp.LateFeeApplyOnFeeHeads.Any(ids.Contains))
				result.Warnings.Add("No late-fee heads overlap. No late fee applies; Collection Policy channel controls remain attached.");
			if (pp == null || ids.Any(x => pp.Rules.All(r => r.FeeHeadId != x)))
				result.Warnings.Add("Heads without a proration rule use full charge. Review this treatment before publishing.");
		}
		catch (JsonException) { result.Errors.Add("A policy snapshot cannot be read. Repair the policy version before publishing."); }
		return result;
	}

	private async Task<List<FinancePolicyVersion>> ActivePolicyVersionsAsync(Guid yearId)
	{
		var collection = (await collectionPolicyRepository.GetListAsync(x => x.AcademicYearId == yearId && x.Status == CollectionPolicyStatus.Active)).Select(x => x.Id).ToList();
		var concession = (await concessionPolicyRepository.GetListAsync(x => x.AcademicYearId == yearId && x.Status == ConcessionPolicyStatus.Active)).Select(x => x.Id).ToList();
		var refund = (await refundPolicyRepository.GetListAsync(x => x.AcademicYearId == yearId && x.Status == RefundPolicyStatus.Active)).Select(x => x.Id).ToList();
		var proration = (await prorationPolicyRepository.GetListAsync(x => x.AcademicYearId == yearId && x.Status == ProrationPolicyStatus.Active)).Select(x => x.Id).ToList();
		return await financePolicyVersionRepository.GetListAsync(x => x.AcademicYearId == yearId && x.Status == "Active" &&
			(x.PolicyType == "collection" && collection.Contains(x.PolicyId) || x.PolicyType == "concession" && concession.Contains(x.PolicyId) ||
			 x.PolicyType == "refund" && refund.Contains(x.PolicyId) || x.PolicyType == "proration" && proration.Contains(x.PolicyId)));
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.Add)]
	public async Task<CollectionPolicyAttachmentImpactResponse> ValidateCollectionPolicyAttachmentAsync(CollectionPolicyAttachmentImpactRequest input)
	{
		var metadata = await GetMetadataReferenceAsync(input.FeeStructureMetadataId);
		await scopeAccess.EnsureCurrentUserCanAccessAsync(new StructureScope(metadata.AcademicYearId, metadata.AcademicBoardId, metadata.AcademicMediumId, null, null));
		var headIds = input.FeeHeadIds.Distinct().ToList();
		var heads = await feeHeadRepository.GetListAsync(x => headIds.Contains(x.Id));
		if (heads.Count != headIds.Count || heads.Any(x => !x.IsActive || x.AcademicYearId != metadata.AcademicYearId))
			throw new BusinessException(CollectionPolicyErrorCodes.InvalidFeeHeadReference);
		var versions = await ActivePolicyVersionsAsync(metadata.AcademicYearId!.Value);
		var version = versions.Where(x => x.PolicyType == "collection" && x.PolicyId == input.CollectionPolicyId).OrderByDescending(x => x.VersionNumber).FirstOrDefault()
			?? throw new BusinessException(CollectionPolicyErrorCodes.PolicyNotActive);
		var policy = CollectionPolicySnapshot.Deserialize(version.SnapshotJson);
		var ids = input.FeeHeadIds.Distinct().ToList();
		var result = new CollectionPolicyAttachmentImpactResponse
		{
			PolicyId = version.PolicyId, PolicyVersionId = version.Id, PolicyName = version.PolicyName, PolicyCode = policy.PolicyCode, IsAttachable = true,
			CoveredLateFeeHeadIds = ids.Intersect(policy.LateFeeApplyOnFeeHeads).ToList(), StructureHeadsWithoutLateFee = ids.Except(policy.LateFeeApplyOnFeeHeads).ToList(),
			PolicyLateFeeHeadsNotInStructure = policy.LateFeeApplyOnFeeHeads.Except(ids).ToList(),
			CoveredWaiverHeadIds = policy.EnableWaiver ? ids.Intersect(policy.LateFeeApplyOnFeeHeads).ToList() : [],
			StructureHeadsWithoutWaiver = policy.EnableWaiver ? ids.Except(policy.LateFeeApplyOnFeeHeads).ToList() : ids,
			PolicyWaiverHeadsNotInStructure = policy.EnableWaiver ? policy.LateFeeApplyOnFeeHeads.Except(ids).ToList() : []
		};
		if (policy.EnableLateFee && result.CoveredLateFeeHeadIds.Count == 0) result.Warnings.Add("No late fee will be charged: no policy late-fee heads overlap this structure.");
		if (result.PolicyLateFeeHeadsNotInStructure.Count > 0) result.Warnings.Add("Some policy Fee Heads are absent from this structure.");
		return result;
	}
}

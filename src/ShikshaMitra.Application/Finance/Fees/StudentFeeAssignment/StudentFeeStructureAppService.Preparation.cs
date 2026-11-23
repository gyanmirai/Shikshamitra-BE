using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms.Services;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using ShikshaMitra.Finance.Policies;
using ShikshaMitra.TenantSettings.EducationBoards;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

public partial class StudentFeeStructureAppService
{
	private async Task<PreparedAssignment> PrepareForContextAsync(AssignStudentFeeStructureDto input, AdmissionAssignmentContext? admission, StudentFeeAssignmentPreviewDto? previousAssignment = null)
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
		var structure = await LoadPreviewEntityAsync(feeStructureRepository, previewReadBatch.Value?.Structures, input.StandardFeeStructureId);
		var metadata = await LoadPreviewEntityAsync(feeStructureMetadataRepository, previewReadBatch.Value?.Metadata, structure.FeeStructureMetadataId);
		if (!structure.IsLocked || structure.StandardId != standardId || metadata.AcademicYearId != input.AcademicYearId ||
			(admission != null && (metadata.AcademicBoardId != admission.BoardId || metadata.AcademicMediumId != admission.MediumId || admission.StructureRevision.HasValue && structure.RevisionNumber != admission.StructureRevision)))
			throw new BusinessException(AssignmentErrorCodes.StructureMismatch);
		var year = await LoadPreviewEntityAsync(academicYearRepository, previewReadBatch.Value?.Years, input.AcademicYearId);
		EducationBoard? board = null;
		if (metadata.AcademicBoardId is Guid boardId &&
			(input.ChargePeriodSource == "BoardAcademicYear" || input.ServicePeriods.Any(p => p.Source is "BoardAcademicYear" or "Registration")))
			board = await LoadAssignmentBoardAsync(boardId);
		var details = await LoadAssignmentDetailsAsync(structure.Id);
		if (details.Count == 0 || details.Select(x => x.FeeHeadId).Distinct().Count() != details.Count ||
			input.SelectedOptionalFeeHeadIds.Any(id => !details.Any(d => d.FeeHeadId == id && d.IsOptional)) ||
			input.FeeHeadDetails.Any(x => !details.Any(d => d.FeeHeadId == x.FeeHeadId && !d.IsVariable && (!d.IsOptional || input.SelectedOptionalFeeHeadIds.Contains(d.FeeHeadId)))) ||
			input.ServicePeriods.Any(x => !details.Any(d => d.FeeHeadId == x.FeeHeadId && (!d.IsOptional || input.SelectedOptionalFeeHeadIds.Contains(d.FeeHeadId)))))
			throw new BusinessException(AssignmentErrorCodes.InvalidInput);
		if (!details.Any(d => !d.IsOptional || input.SelectedOptionalFeeHeadIds.Contains(d.FeeHeadId)))
			throw new BusinessException(AssignmentErrorCodes.InvalidInput)
				.WithData("Reason", "Select at least one fee before assigning.");
		if (input.VariableFeeHeads.Any(x => !details.Any(d => d.FeeHeadId == x.FeeHeadId && d.IsVariable &&
                (!d.IsOptional || input.SelectedOptionalFeeHeadIds.Contains(d.FeeHeadId)))))
            throw new BusinessException(AssignmentErrorCodes.InvalidInput);
        foreach (var detail in details.Where(d => structure.FeeStructureDetails.All(x => x.Id != d.Id))) structure.AddDetail(detail);
		var selectedPolicies = await ResolveAssignedPoliciesAsync(structure, year.Id, input.Policies, input.PolicyOverrideReason, input.EditAssignmentId, policyReads: previewReadBatch.Value?.Policies);
		selectedPolicies["proration"] = admission != null ? admission.Proration :
			await LazyServiceProvider.LazyGetRequiredService<AdmissionProrationPolicyResolver>().ResolveAsync(year.Id, structure.Id);
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
		var headRecords = await LoadAssignmentHeadsAsync(headIds);
		var financeAssignment = admission == null || admission.IsFinanceAssignment;
		var scheduleResolver = LazyServiceProvider.LazyGetRequiredService<PaymentTermScheduleResolver>();
		var applicableProration = prorationRules?.Rules.Any(r => details.Any(d => d.FeeHeadId == r.FeeHeadId && !d.IsOneTime &&
			(!d.IsOptional || input.SelectedOptionalFeeHeadIds.Contains(d.FeeHeadId))) && r.ApplyOnAdmission && r.RuleType != ProrationRuleType.FullCharge) == true;
		if (admission != null && applicableProration && !admission.JoiningDate.HasValue)
			throw new BusinessException(ProrationPolicyErrorCodes.JoiningDateRequired);
		if (admission?.JoiningDate?.Date > year.EndDate.Date)
			throw new BusinessException(ProrationPolicyErrorCodes.JoiningDateInvalid);
		var admissionStart = admission?.JoiningDate?.Date > year.StartDate.Date ? admission.JoiningDate.Value.Date : year.StartDate.Date;
		var hasBoardDates = board != null && HasAssignmentBoardDates(board, year);
		var registrationStart = hasBoardDates && board!.StartDate.Date > admissionStart ? board.StartDate.Date : admissionStart;
		var registrationEnd = hasBoardDates ? board!.EndDate.Date : year.EndDate.Date;
		if (admission is { IsFinanceAssignment: true, JoiningDate: not null } && registrationStart > registrationEnd)
			throw new BusinessException(ProrationPolicyErrorCodes.JoiningDateInvalid);
		var usesRegistrationDates = admission is { IsFinanceAssignment: true, JoiningDate: not null } &&
			input.ServicePeriods.Any(p => p.Source == "Registration");
		var names = headRecords.ToDictionary(h => h.Key, h => h.Value.Name);
		var preview = new StudentFeeAssignmentPreviewDto {
			CalculationVersion = "assignment-v3",
			AssignmentSource = admission == null ? "Finance" : "Admission", ApplicationId = admission?.ApplicationId,
			StudentId = studentId, AcademicYearId = year.Id, EnrollmentId = enrollment?.Id,
			StructureId = structure.Id, StructureName = structure.Name, RevisionNumber = structure.RevisionNumber,
			RegistrationJoiningDate = admission?.JoiningDate,
			ChargeStartDate = DateTime.SpecifyKind(usesRegistrationDates ? registrationStart :
				admission != null && applicableProration ? admissionStart : year.StartDate.Date, DateTimeKind.Utc),
			ChargeEndDate = DateTime.SpecifyKind(usesRegistrationDates ? registrationEnd : year.EndDate.Date, DateTimeKind.Utc),
			OverrideReason = input.OverrideReason?.Trim(),
			PolicyOverrideReason = input.PolicyOverrideReason?.Trim(),
			ProrationPolicyId = proration?.PolicyId, ProrationPolicyVersionId = proration?.Id
		};
		var terms = await LoadAssignmentTermsAsync(details.Where(detail => !detail.IsOptional || input.SelectedOptionalFeeHeadIds.Contains(detail.FeeHeadId)));
		var optionalDetails = new List<CustomOptionalFeeHeadDetail>();
		foreach (var detail in details)
		{
			var included = !detail.IsOptional || input.SelectedOptionalFeeHeadIds.Contains(detail.FeeHeadId);
			var configuredAmount = detail.MonthlyRate ?? detail.Amount;
			var head = new AssignmentHeadDto { FeeHeadId = detail.FeeHeadId, FeeHeadName = names.GetValueOrDefault(detail.FeeHeadId, "Fee head"),
				IsOptional = detail.IsOptional, IsVariable = detail.IsVariable, IsOneTime = detail.IsOneTime, Included = included, ConfiguredAmount = configuredAmount,
				EffectiveAmount = included ? configuredAmount : null, AmountBasis = detail.MonthlyRate.HasValue ? "MonthlyRate" : detail.IsVariable ? "CoverageTotal" : detail.IsOneTime ? "OneTime" : "AnnualTotal",
				PaymentTermId = detail.PaymentTermId, PaymentTermVersionId = detail.PaymentTermVersionId };
			preview.Heads.Add(head);
			if (!included) continue;
			// New assignments must preserve an explicit published term, including one-time heads.
			if (!detail.PaymentTermId.HasValue || !detail.PaymentTermVersionId.HasValue)
				throw new BusinessException(AssignmentErrorCodes.ConfigurationInvalid).WithData("Reason", "Publish a payment term for every included fee head.");
			if (!terms.TryGetValue(detail.PaymentTermId.Value, out var term))
				throw new BusinessException(AssignmentErrorCodes.ConfigurationInvalid);
			var termVersion = term.Versions.SingleOrDefault(version => version.Id == detail.PaymentTermVersionId.Value);
			if (termVersion == null || termVersion.PaymentTermId != term.Id || term.AcademicYearId != year.Id)
				throw new BusinessException(AssignmentErrorCodes.ConfigurationInvalid);
			head.PaymentTermName = term.Name;
			head.PaymentTermVersionNumber = termVersion.VersionNumber;
			if (detail.IsVariable)
            {
                var variable = input.VariableFeeHeads.SingleOrDefault(x => x.FeeHeadId == detail.FeeHeadId);
                if (variable?.Amount == null)
                    throw new BusinessException(AssignmentErrorCodes.VariableAmountRequired).WithData("FeeHeadId", detail.FeeHeadId);
                head.EffectiveAmount = variable.Amount;
                if (variable.IsMonthlyRate)
                    head.AmountBasis = "MonthlyRate";
                head.ZeroAmountReason = variable.ZeroAmountReason?.Trim();
            }
			var prior = previousAssignment?.StructureId == structure.Id
				? previousAssignment.Heads.SingleOrDefault(saved => saved.FeeHeadId == detail.FeeHeadId && saved.Included)
				: null;
			var fixedAmount = prior?.EffectiveAmount ?? configuredAmount;
			var amountInput = input.FeeHeadDetails.FirstOrDefault(x => x.FeeHeadId == detail.FeeHeadId);
			if (amountInput != null)
			{
				if (amountInput.PayableMonth.HasValue || amountInput.StartPayableMonth.HasValue || amountInput.EndPayableMonth.HasValue ||
					amountInput.ChargeStartDate.HasValue || amountInput.ChargeEndDate.HasValue)
					throw new BusinessException(AssignmentErrorCodes.InvalidInput);
				if (amountInput.Amount != fixedAmount)
					throw new BusinessException(AssignmentErrorCodes.InvalidInput).WithData("FeeHeadId", detail.FeeHeadId);
			}
			if (!detail.IsVariable) head.EffectiveAmount = fixedAmount;
			var priorPeriod = prior?.ServicePeriod;
			var lockRequiredDates = previousAssignment != null && !detail.IsOptional;
			var requested = input.ServicePeriods.FirstOrDefault(x => x.FeeHeadId == detail.FeeHeadId)
				?? (lockRequiredDates ? priorPeriod : null)
				?? new AssignmentServicePeriodDto { FeeHeadId = detail.FeeHeadId, Source = input.ChargePeriodSource,
					StartDate = input.ChargeStartDate, EndDate = input.ChargeEndDate, SourceReferenceId = input.ChargePeriodSourceReferenceId, Reason = input.ChargePeriodOverrideReason };
			if (requested.Source == "TransportService" && (detail.IsVariable || headRecords[detail.FeeHeadId].Module != ShikshaMitra.Finance.Fees.Settings.FeeHeadModule.Transport))
				throw new BusinessException(AssignmentErrorCodes.InvalidInput);
			if (admission != null && !admission.IsFinanceAssignment && requested.Source != "AcademicYear") throw new BusinessException(AssignmentErrorCodes.SourceUnavailable);
			var admissionRule = prorationRules?.Rules.FirstOrDefault(r => r.FeeHeadId == detail.FeeHeadId);
			var useAdmission = admission != null && !detail.IsOneTime && admissionRule is { ApplyOnAdmission: true } && admissionRule.RuleType != ProrationRuleType.FullCharge;
			var singleServiceMonth = financeAssignment && (detail.IsOneTime ||
				admission?.JoiningDate != null && !detail.IsOptional &&
				IsFullChargeForServiceDates(admissionRule) &&
				IsSingleAnnualInstallment(scheduleResolver.ResolveFromTerms(detail, terms).Installments,
					hasBoardDates ? board!.StartDate : year.StartDate, registrationEnd));
			var savedPeriod = priorPeriod != null && priorPeriod.Source == requested.Source &&
				priorPeriod.SourceReferenceId == requested.SourceReferenceId &&
				priorPeriod.StartDate?.Date == requested.StartDate?.Date && priorPeriod.EndDate?.Date == requested.EndDate?.Date;
			var preserveRequiredDates = lockRequiredDates && priorPeriod != null;
			if (lockRequiredDates && (preserveRequiredDates
				? !savedPeriod || priorPeriod!.Reason != requested.Reason
				: requested.Source is "ManualOverride" or "TransportService"))
				throw new BusinessException(AssignmentErrorCodes.InvalidInput).WithData("FeeHeadId", detail.FeeHeadId);
			(AssignmentServicePeriodDto Period, string Verification, string? Revision) fact;
			if (preserveRequiredDates)
				fact = (priorPeriod!, prior!.Verification, prior.SourceRevision);
			else if (requested.Source == "Registration" && admission is { IsFinanceAssignment: true, JoiningDate: not null })
			{
				var start = registrationStart;
				var end = singleServiceMonth && (detail.IsOneTime || requested.EndDate?.Date != registrationEnd)
					? EndOfAssignmentServiceMonth(start, registrationEnd)
					: registrationEnd;
				var keepSaved = savedPeriod && previousAssignment!.RegistrationJoiningDate?.Date == admission.JoiningDate.Value.Date;
				if (requested.SourceReferenceId != admission.ApplicationId || start > end ||
					(!keepSaved && (requested.StartDate.HasValue && requested.StartDate.Value.Date != start ||
						requested.EndDate.HasValue && requested.EndDate.Value.Date != end)))
					throw new BusinessException(AssignmentErrorCodes.SourceUnavailable);
				fact = (keepSaved ? priorPeriod! : new AssignmentServicePeriodDto
				{
					FeeHeadId = detail.FeeHeadId, Source = "Registration", SourceReferenceId = admission.ApplicationId,
					StartDate = DateTime.SpecifyKind(start, DateTimeKind.Utc), EndDate = DateTime.SpecifyKind(end, DateTimeKind.Utc)
				}, "Verified admission joining date", admission.SourceRevision);
			}
			else if (useAdmission && requested.Source is "AcademicYear" or "BoardAcademicYear")
			{
				await ResolveServiceAsync(input.StudentId, requested, year, board);
				fact = (new AssignmentServicePeriodDto { FeeHeadId = detail.FeeHeadId, Source = "Registration", SourceReferenceId = admission!.ApplicationId,
					StartDate = DateTime.SpecifyKind(admissionStart, DateTimeKind.Utc), EndDate = DateTime.SpecifyKind(year.EndDate.Date, DateTimeKind.Utc) },
					"Verified admission joining date", admission.SourceRevision);
			}
			else
			{
				var scopePeriod = singleServiceMonth && savedPeriod &&
					(requested.Source is "AcademicYear" or "BoardAcademicYear")
					? new AssignmentServicePeriodDto { FeeHeadId = requested.FeeHeadId, Source = requested.Source,
						SourceReferenceId = requested.SourceReferenceId }
					: requested;
				fact = await ResolveServiceAsync(input.StudentId, scopePeriod, year, board);
				if (singleServiceMonth && !savedPeriod)
				{
					var start = fact.Period.StartDate!.Value;
					var end = fact.Period.EndDate!.Value;
					var monthEnd = EndOfAssignmentServiceMonth(start, end);
					if (requested.Source == "ManualOverride" && end != monthEnd)
						throw new BusinessException(AssignmentErrorCodes.OneTimeServiceMonth);
					if (requested.Source != "ManualOverride")
						fact.Period.EndDate = monthEnd;
				}
				else if (!ReferenceEquals(scopePeriod, requested))
					fact.Period.EndDate = priorPeriod!.EndDate;
			}
			head.ServicePeriod = fact.Period;
			head.Verification = detail.IsVariable && !preserveRequiredDates
				? "Student amount entered during assignment; " + fact.Verification : fact.Verification;
			head.SourceRevision = fact.Revision;
			optionalDetails.Add(new CustomOptionalFeeHeadDetail { FeeHeadId = detail.FeeHeadId, Amount = head.EffectiveAmount ?? throw new BusinessException(AssignmentErrorCodes.VariableAmountRequired),
				IsMonthlyRate = head.AmountBasis == "MonthlyRate", ChargeStartDate = fact.Period.StartDate, ChargeEndDate = fact.Period.EndDate });
		}
		var calculatedDues = previousAssignment != null
			? await dueFeeManager.CalculateForStudentAsync(studentId, structure, year,
				input.SelectedOptionalFeeHeadIds, optionalDetails, prorationRules, proration?.Id, year.StartDate, year.EndDate,
				financeFeeAccountId: admission?.AccountId, paymentTerms: terms)
			: await dueFeeManager.GenerateForStudentAsync(studentId, structure, year,
				input.SelectedOptionalFeeHeadIds, optionalDetails, prorationRules, proration?.Id, year.StartDate, year.EndDate,
				financeFeeAccountId: admission?.AccountId, paymentTerms: terms);
		var dues = calculatedDues.OrderBy(x => x.FeeHeadId).ThenBy(x => x.InstallmentSequence).ToList();
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
			policy.Source = version.PolicyType == "proration"
				? admission?.IsProrationOverride == true ? "AdmissionOverride" :
					structure.GetExplicitProrationPolicyVersionId() == version!.Id ? "FeeStructure" : "FinanceDefault"
				: StructurePolicyVersions(structure)[version.PolicyType] == version.Id ? "FeeStructure" : "StudentOverride";
			foreach (var head in preview.Heads)
				policy.HeadTreatment[head.FeeHeadId] = !head.Included ? "Not included" : version.PolicyType switch {
					"collection" => collectionRules.EnableLateFee && (collectionRules.ApplyLateFeeToAllFeeHeads || collectionRules.LateFeeApplyOnFeeHeads.Contains(head.FeeHeadId))
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

	private static bool IsSingleAnnualInstallment(IReadOnlyList<ResolvedInstallment> installments, DateTime start, DateTime end) =>
		installments.Count == 1 && installments[0].CoverageStartDate.Date <= start.Date &&
		installments[0].CoverageEndDate.Date >= end.Date;

	private static bool IsFullChargeForServiceDates(ProrationPolicyRuleSnapshot? rule) =>
		rule == null ||
		(!rule.ApplyOnAdmission || rule.RuleType == ProrationRuleType.FullCharge) &&
		(!rule.ApplyOnEarlyWithdrawal || (rule.EarlyWithdrawalRuleType ?? rule.RuleType) == ProrationRuleType.FullCharge);

	private static DateTime EndOfAssignmentServiceMonth(DateTime start, DateTime end)
	{
		var monthEnd = start.Date.AddDays(DateTime.DaysInMonth(start.Year, start.Month) - start.Day);
		return monthEnd < end ? monthEnd : end;
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

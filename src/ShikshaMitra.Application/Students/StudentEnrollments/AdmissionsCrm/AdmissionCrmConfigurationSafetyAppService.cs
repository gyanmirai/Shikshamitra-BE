using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
[RemoteService(false)]
public class AdmissionCrmConfigurationSafetyAppService(
	AdmissionCrmConfigurationSnapshotReader snapshotReader,
	AdmissionCrmConfigurationCloneWriter cloneWriter,
	StudentScopeAccessService studentScopeAccessService,
	IAbpDistributedLock distributedLock,
	IUnitOfWorkManager unitOfWorkManager)
	: ApplicationService, IAdmissionCrmConfigurationSafetyAppService
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private static readonly Regex GuidPattern = new(
		@"(?<![0-9a-fA-F])[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}(?![0-9a-fA-F])",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	public async Task<AdmissionCrmConfigurationReferenceResultDto> GetReferencesAsync(
		Guid academicYearId,
		AdmissionCrmConfigurationReferenceKind kind,
		string code)
	{
		var normalizedCode = NormalizeCode(code);
		await EnsureAcademicYearAccessAsync(academicYearId, nameof(GetReferencesAsync));
		var snapshot = await snapshotReader.LoadAsync(academicYearId);
		var families = await BuildReferenceFamiliesAsync(snapshot, kind, normalizedCode);
		return new AdmissionCrmConfigurationReferenceResultDto
		{
			AcademicYearId = academicYearId,
			Kind = kind,
			Code = normalizedCode,
			TotalReferenceCount = families.Sum(x => x.ReferenceCount),
			Families = families
		};
	}

	public async Task<AdmissionCrmConfigurationImpactPreviewDto> PreviewImpactAsync(
		Guid academicYearId,
		PreviewAdmissionCrmConfigurationImpactDto input)
	{
		return await BuildImpactPreviewAsync(
			academicYearId,
			input,
			nameof(PreviewImpactAsync),
			enforceAccess: true);
	}

	internal async Task EnsureImpactFingerprintAsync(
		Guid academicYearId,
		PreviewAdmissionCrmConfigurationImpactDto input,
		string? submittedFingerprint,
		string operation)
	{
		if (string.IsNullOrWhiteSpace(submittedFingerprint))
		{
			throw new BusinessException("AdmissionCrm:ConfigurationImpactPreviewRequired")
				.WithData("Operation", operation);
		}

		var preview = await BuildImpactPreviewAsync(
			academicYearId,
			input,
			operation,
			enforceAccess: false);
		if (!string.Equals(preview.Fingerprint, submittedFingerprint.Trim(), StringComparison.Ordinal))
		{
			throw new BusinessException("AdmissionCrm:ConfigurationImpactFingerprintMismatch")
				.WithData("ExpectedFingerprint", preview.Fingerprint)
				.WithData("Operation", operation);
		}
	}

	internal async Task<string> CreateCycleImpactFingerprintAsync(
		Guid academicYearId,
		UpdateAdmissionCrmCycleConfigurationDto input,
		string operation)
	{
		var preview = await BuildImpactPreviewAsync(
			academicYearId,
			new PreviewAdmissionCrmConfigurationImpactDto
			{
				ProposedCycleConfiguration = input
			},
			operation,
			enforceAccess: false);
		return preview.Fingerprint;
	}

	private async Task<AdmissionCrmConfigurationImpactPreviewDto> BuildImpactPreviewAsync(
		Guid academicYearId,
		PreviewAdmissionCrmConfigurationImpactDto input,
		string operation,
		bool enforceAccess)
	{
		ValidateImpactInput(input);
		if (enforceAccess)
		{
			await EnsureAcademicYearAccessAsync(academicYearId, operation);
		}
		var snapshot = await snapshotReader.LoadAsync(academicYearId);
		var proposedStage = input.PipelineStageId.HasValue
			? snapshot.PipelineStages.SingleOrDefault(x => x.Id == input.PipelineStageId.Value)
			: null;
		if (input.PipelineStageId.HasValue && proposedStage == null)
		{
			throw new BusinessException("AdmissionCrm:PipelineStageNotFound")
				.WithData("PipelineStageId", input.PipelineStageId.Value)
				.WithData("AcademicYearId", academicYearId);
		}

		var effectiveKind = proposedStage == null
			? input.Kind
			: AdmissionCrmConfigurationReferenceKind.Stage;
		var effectiveCode = proposedStage?.StageKey ?? input.Code;
		var applicantCount = await snapshotReader.CountApplicantsAsync(academicYearId);
		var referenceFamilies = effectiveKind.HasValue
			? await BuildReferenceFamiliesAsync(snapshot, effectiveKind.Value, NormalizeCode(effectiveCode!))
			: [];
		var changedFields = GetChangedFields(snapshot.Cycle, input.ProposedCycleConfiguration)
			.Concat(GetChangedFields(proposedStage, input.ProposedPipelineStage))
			.Distinct(StringComparer.Ordinal)
			.OrderBy(x => x, StringComparer.Ordinal)
			.ToList();
		var referenceCount = referenceFamilies.Sum(x => x.ReferenceCount);
		var warnings = BuildImpactWarnings(
			applicantCount,
			referenceCount,
			changedFields,
			effectiveKind,
			effectiveCode);
		var fingerprint = ComputeFingerprint(JsonSerializer.Serialize(new
		{
			Type = "AdmissionCrmConfigurationImpact",
			AcademicYearId = academicYearId,
			Snapshot = BuildSnapshotSignature(snapshot),
			ApplicantCount = applicantCount,
			Input = BuildImpactInputSignature(
				effectiveKind,
				effectiveCode,
				input.ProposedCycleConfiguration,
				input.PipelineStageId,
				input.ProposedPipelineStage),
			ChangedFields = changedFields,
			ReferenceCount = referenceCount
		}, JsonOptions));

		return new AdmissionCrmConfigurationImpactPreviewDto
		{
			AcademicYearId = academicYearId,
			AffectedApplicantCount = changedFields.Count == 0 && !effectiveKind.HasValue ? 0 : applicantCount,
			ConfigurationReferenceCount = referenceCount,
			ChangedFields = changedFields,
			ReferenceFamilies = referenceFamilies,
			Warnings = warnings,
			Fingerprint = fingerprint
		};
	}

	public async Task<AdmissionCrmConfigurationClonePreviewDto> PreviewCloneAsync(
		PreviewAdmissionCrmConfigurationCloneDto input)
	{
		return await BuildClonePreviewAsync(input, nameof(PreviewCloneAsync));
	}

	[UnitOfWork(IsDisabled = true)]
	public async Task<AdmissionCrmConfigurationCloneResultDto> ConfirmCloneAsync(
		ConfirmAdmissionCrmConfigurationCloneDto input)
	{
		ValidateCloneYearPair(input.SourceAcademicYearId, input.TargetAcademicYearId);
		var lockKey =
			$"AdmissionCrm:ConfigurationClone:{CurrentTenant.Id?.ToString("D") ?? "host"}:{input.TargetAcademicYearId:D}";
		await using var lockHandle = await distributedLock.TryAcquireAsync(
			lockKey,
			TimeSpan.FromSeconds(30));
		if (lockHandle == null)
		{
			throw new BusinessException("AdmissionCrm:ConfigurationCloneBusy")
				.WithData("TargetAcademicYearId", input.TargetAcademicYearId);
		}

		using var unitOfWork = unitOfWorkManager.Begin(
			new AbpUnitOfWorkOptions { IsTransactional = true },
			requiresNew: true);
		var result = await ConfirmCloneCoreAsync(input);
		await unitOfWork.CompleteAsync();
		return result;
	}

	private async Task<AdmissionCrmConfigurationCloneResultDto> ConfirmCloneCoreAsync(
		ConfirmAdmissionCrmConfigurationCloneDto input)
	{
		await EnsureAcademicYearAccessAsync(input.SourceAcademicYearId, nameof(ConfirmCloneAsync));
		var targetAcademicYear = await EnsureAcademicYearAccessAsync(
			input.TargetAcademicYearId,
			nameof(ConfirmCloneAsync));
		var source = await snapshotReader.LoadAsync(input.SourceAcademicYearId);
		var target = await snapshotReader.LoadAsync(input.TargetAcademicYearId);

		if (TryGetCloneMetadata(target.Cycle, out var clonedSourceId, out var clonedFingerprint))
		{
			if (clonedSourceId == input.SourceAcademicYearId &&
			    string.Equals(clonedFingerprint, input.PreviewFingerprint, StringComparison.Ordinal))
			{
				var existingHierarchyMapping = BuildHierarchyMapping(source, target);
				return new AdmissionCrmConfigurationCloneResultDto
				{
					SourceAcademicYearId = input.SourceAcademicYearId,
					TargetAcademicYearId = input.TargetAcademicYearId,
					AlreadyApplied = true,
					Families = BuildCloneFamilies(source, target, existingHierarchyMapping),
					Warnings = BuildCloneWarnings(source, existingHierarchyMapping),
					PreviewFingerprint = input.PreviewFingerprint
				};
			}

			throw new BusinessException("AdmissionCrm:ConfigurationCloneFingerprintMismatch");
		}

		var targetApplicantCount = await snapshotReader.CountApplicantsAsync(input.TargetAcademicYearId);
		var preview = BuildClonePreview(source, target, targetApplicantCount);
		if (!string.Equals(preview.Fingerprint, input.PreviewFingerprint, StringComparison.Ordinal))
		{
			throw new BusinessException("AdmissionCrm:ConfigurationCloneFingerprintMismatch")
				.WithData("ExpectedFingerprint", preview.Fingerprint);
		}
		if (!preview.CanConfirm)
		{
			throw new BusinessException(GetCloneConflictErrorCode(preview))
				.WithData("Conflicts", string.Join(" ", preview.Conflicts));
		}

		var hierarchyMapping = BuildHierarchyMapping(source, target);
		await cloneWriter.WriteAsync(
			source,
			targetAcademicYear,
			input.PreviewFingerprint,
			hierarchyMapping.IdMap);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return new AdmissionCrmConfigurationCloneResultDto
		{
			SourceAcademicYearId = input.SourceAcademicYearId,
			TargetAcademicYearId = input.TargetAcademicYearId,
			AlreadyApplied = false,
			Families = preview.Families
				.Select(x => new AdmissionCrmConfigurationCloneFamilyDto
				{
					Family = x.Family,
					SourceCount = x.SourceCount,
					CloneableCount = x.CloneableCount,
					TargetCount = x.CloneableCount,
					CloneBehavior = x.CloneBehavior
				})
				.ToList(),
			Warnings = preview.Warnings,
			PreviewFingerprint = input.PreviewFingerprint
		};
	}

	private async Task<AdmissionCrmConfigurationClonePreviewDto> BuildClonePreviewAsync(
		PreviewAdmissionCrmConfigurationCloneDto input,
		string operation)
	{
		ValidateCloneYearPair(input.SourceAcademicYearId, input.TargetAcademicYearId);
		await EnsureAcademicYearAccessAsync(input.SourceAcademicYearId, operation);
		await EnsureAcademicYearAccessAsync(input.TargetAcademicYearId, operation);
		var source = await snapshotReader.LoadAsync(input.SourceAcademicYearId);
		var target = await snapshotReader.LoadAsync(input.TargetAcademicYearId);
		var targetApplicantCount = await snapshotReader.CountApplicantsAsync(input.TargetAcademicYearId);
		return BuildClonePreview(source, target, targetApplicantCount);
	}

	private static AdmissionCrmConfigurationClonePreviewDto BuildClonePreview(
		AdmissionCrmConfigurationSnapshot source,
		AdmissionCrmConfigurationSnapshot target,
		int targetApplicantCount)
	{
		var conflicts = new List<string>();
		if (source.Cycle == null)
		{
			conflicts.Add("The source academic year has no Admission Year settings to clone.");
		}
		if (targetApplicantCount > 0)
		{
			conflicts.Add(
				$"The target academic year has {targetApplicantCount} applicant record(s); configuration cloning requires no applicant activity.");
		}
		if (target.ConfigurationEntityCount > 0)
		{
			conflicts.Add(
				$"The target academic year already has {target.ConfigurationEntityCount} admission configuration record(s).");
		}

		var hierarchyMapping = BuildHierarchyMapping(source, target);
		conflicts.AddRange(hierarchyMapping.Conflicts);
		var families = BuildCloneFamilies(source, target, hierarchyMapping);
		var warnings = BuildCloneWarnings(source, hierarchyMapping);
		var fingerprint = ComputeFingerprint(JsonSerializer.Serialize(new
		{
			Type = "AdmissionCrmConfigurationClone",
			SourceAcademicYearId = source.AcademicYear.Id,
			TargetAcademicYearId = target.AcademicYear.Id,
			SourceSnapshot = BuildSnapshotSignature(source),
			TargetSnapshot = BuildSnapshotSignature(target),
			TargetApplicantCount = targetApplicantCount,
			Families = families,
			Conflicts = conflicts
		}, JsonOptions));

		return new AdmissionCrmConfigurationClonePreviewDto
		{
			SourceAcademicYearId = source.AcademicYear.Id,
			TargetAcademicYearId = target.AcademicYear.Id,
			CanConfirm = conflicts.Count == 0,
			TargetApplicantActivityCount = targetApplicantCount,
			Families = families,
			Conflicts = conflicts,
			Warnings = warnings,
			Fingerprint = fingerprint
		};
	}

	private async Task<AcademicYear> EnsureAcademicYearAccessAsync(Guid academicYearId, string operation)
	{
		await studentScopeAccessService.EnsureCanAccessAcademicYearAsync(academicYearId, operation);
		if (!await studentScopeAccessService.CurrentUserHasAcademicYearWideScopeAsync(academicYearId))
		{
			throw new BusinessException("AdmissionCrm:AcademicYearWideScopeRequired")
				.WithData("AcademicYearId", academicYearId)
				.WithData("Operation", operation);
		}
		return await snapshotReader.GetAcademicYearAsync(academicYearId);
	}

	private async Task<IReadOnlyList<AdmissionCrmConfigurationReferenceFamilyDto>> BuildReferenceFamiliesAsync(
		AdmissionCrmConfigurationSnapshot snapshot,
		AdmissionCrmConfigurationReferenceKind kind,
		string code)
	{
		var families = new List<AdmissionCrmConfigurationReferenceFamilyDto>();
		void Add(string family, int count, string description, IEnumerable<Guid>? ids = null)
		{
			families.Add(new AdmissionCrmConfigurationReferenceFamilyDto
			{
				Family = family,
				ReferenceCount = count,
				Description = description,
				SampleReferenceIds = (ids ?? [])
					.Distinct()
					.OrderBy(x => x)
					.Take(25)
					.ToList()
			});
		}

		var cycleCount = snapshot.Cycle == null
			? 0
			: CountMatches(code,
				snapshot.Cycle.LeadScoringRulesJson,
				snapshot.Cycle.LeadRoutingRulesJson,
				snapshot.Cycle.JourneyRulesJson,
				snapshot.Cycle.ApplicantChecklistJson,
				snapshot.Cycle.ChannelSettingsJson,
				snapshot.Cycle.SlaPolicyJson);
		Add(
			"Cycle configuration",
			cycleCount,
			"Routing, journey, channel, checklist, scoring, and SLA settings that reference this code.",
			cycleCount > 0 && snapshot.Cycle != null ? [snapshot.Cycle.Id] : []);

		var stageIds = snapshot.PipelineStages
			.Where(x =>
				kind == AdmissionCrmConfigurationReferenceKind.Stage &&
				string.Equals(x.StageKey, code, StringComparison.OrdinalIgnoreCase))
			.Select(x => x.Id)
			.ToList();
		var stageCount = stageIds.Count +
			snapshot.PipelineStages.Sum(x => CountMatches(code, x.AllowedNextStagesJson));
		Add(
			"Pipeline stage definitions",
			stageCount,
			"Stage definitions and allowed-transition settings that reference this code.",
			stageIds);

		var sourceIds = snapshot.InquirySources
			.Where(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase))
			.Select(x => x.Id)
			.ToList();
		Add(
			"Inquiry sources",
			sourceIds.Count,
			"Inquiry-source definitions with this code.",
			sourceIds);

		var formMatches = snapshot.InquiryForms
			.Select(x => new
			{
				x.Id,
				Count = CountMatches(code, x.FieldsJson, x.LocalizationsJson)
			})
			.Where(x => x.Count > 0)
			.ToList();
		Add(
			"Inquiry forms",
			formMatches.Sum(x => x.Count),
			"Published or draft inquiry-form fields and localizations that reference this code.",
			formMatches.Select(x => x.Id));

		var messageMatches = snapshot.MessageTemplates
			.Select(x => new
			{
				x.Id,
				Count = CountMatches(code, x.VariablesJson) +
				        CountExactText(code, x.Subject) +
				        CountExactText(code, x.Body)
			})
			.Where(x => x.Count > 0)
			.ToList();
		Add(
			"Message templates",
			messageMatches.Sum(x => x.Count),
			"Message variables or exact template values that reference this code.",
			messageMatches.Select(x => x.Id));

		AddPlaybookReferenceFamily(
			families,
			"Playbook stages",
			"Admission-journey stage definitions that reference this code.",
			snapshot.Playbooks,
			playbook => playbook.StagesJson,
			code);
		AddPlaybookReferenceFamily(
			families,
			"Playbook rounds",
			"Admission-round definitions that reference this code.",
			snapshot.Playbooks,
			playbook => playbook.RoundsJson,
			code);
		AddPlaybookReferenceFamily(
			families,
			"Playbook exams",
			"Exam definitions that reference this code.",
			snapshot.Playbooks,
			playbook => playbook.ExamDefinitionsJson,
			code);
		AddPlaybookReferenceFamily(
			families,
			"Playbook seat plans",
			"Seat-plan definitions that reference this code.",
			snapshot.Playbooks,
			playbook => playbook.SeatPlansJson,
			code);
		var playbookRuleMatches = snapshot.Playbooks
			.Select(x => new
			{
				x.Id,
				Count = CountMatches(
					code,
					x.ApplicabilityJson,
					x.SlotTemplatesJson,
					x.AssignmentRulesJson)
			})
			.Where(x => x.Count > 0)
			.ToList();
		Add(
			"Playbook routing and assignment rules",
			playbookRuleMatches.Sum(x => x.Count),
			"Applicability, slot-template, and assignment rules that reference this code.",
			playbookRuleMatches.Select(x => x.Id));

		var routeDefinitions = snapshot.RouteDefinitions
			.Where(x =>
				kind == AdmissionCrmConfigurationReferenceKind.Route &&
				string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase))
			.ToList();
		var routeConfigurationMatches = snapshot.RouteDefinitions
			.Select(x => new { x.Id, Count = CountMatches(code, x.ConfigurationJson) })
			.Where(x => x.Count > 0)
			.ToList();
		Add(
			"Route definitions",
			routeDefinitions.Count + routeConfigurationMatches.Sum(x => x.Count),
			"Route definitions and immutable route configuration snapshots that reference this code.",
			routeDefinitions.Select(x => x.Id).Concat(routeConfigurationMatches.Select(x => x.Id)));

		var seatPoolMatches = snapshot.SeatPools
			.Where(x =>
				(kind == AdmissionCrmConfigurationReferenceKind.SeatPlan &&
				 string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)) ||
				(kind == AdmissionCrmConfigurationReferenceKind.Round &&
				 string.Equals(x.RoundCode, code, StringComparison.OrdinalIgnoreCase)))
			.ToList();
		Add(
			"Seat pools",
			seatPoolMatches.Count,
			"Operational seat-pool definitions linked to this seat-plan or round code.",
			seatPoolMatches.Select(x => x.Id));

		var registrationMatches = snapshot.RegistrationTemplates
			.Select(x => new
			{
				x.Id,
				Count = CountMatches(code, x.ApplicabilityJson, x.SectionsJson)
			})
			.Where(x => x.Count > 0)
			.ToList();
		var formNumberMatches = snapshot.FormNumberSeries
			.Select(x => new { x.Id, Count = CountMatches(code, x.ApplicabilityJson) })
			.Where(x => x.Count > 0)
			.ToList();
		Add(
			"Registration templates and number series",
			registrationMatches.Sum(x => x.Count) + formNumberMatches.Sum(x => x.Count),
			"Registration sections, applicability rules, and form-number rules that reference this code.",
			registrationMatches.Select(x => x.Id).Concat(formNumberMatches.Select(x => x.Id)));

		var checklistMatches = snapshot.DocumentChecklists
			.Select(x => new
			{
				x.Id,
				Count = CountMatches(code, x.ApplicabilityJson, x.RequirementsJson)
			})
			.Where(x => x.Count > 0)
			.ToList();
		Add(
			"Document checklist templates",
			checklistMatches.Sum(x => x.Count),
			"Document applicability and requirement rules that reference this code.",
			checklistMatches.Select(x => x.Id));

		var feeMatches = snapshot.FeePolicies
			.Select(x => new
			{
				x.Id,
				Count = CountMatches(code, x.ApplicabilityJson, x.ComponentsJson)
			})
			.Where(x => x.Count > 0)
			.ToList();
		Add(
			"Fee policies",
			feeMatches.Sum(x => x.Count),
			"Fee-policy applicability and component rules that reference this code.",
			feeMatches.Select(x => x.Id));

		var adoptionMatches = snapshot.PolicyAdoptions
			.Select(x => new
			{
				x.Id,
				Count = CountMatches(
					code,
					x.ApplicabilityJson,
					x.OverridesJson,
					x.ImpactPreviewJson)
			})
			.Where(x => x.Count > 0)
			.ToList();
		Add(
			"Policy adoptions",
			adoptionMatches.Sum(x => x.Count),
			"Target-year policy applicability, overrides, and impact snapshots that reference this code.",
			adoptionMatches.Select(x => x.Id));

		var policyPackMatches = snapshot.PolicyPacks
			.Select(x => new
			{
				x.Id,
				Count = CountMatches(code, x.SourcesJson, x.RulesJson)
			})
			.Where(x => x.Count > 0)
			.ToList();
		Add(
			"Global policy packs",
			policyPackMatches.Sum(x => x.Count),
			"Shared governed policy packs referenced by this academic year; these are global and are not cloned.",
			policyPackMatches.Select(x => x.Id));

		var routeDefinitionIds = routeDefinitions.Select(x => x.Id).ToList();
		var applicantReferences = await snapshotReader.GetApplicantReferencesAsync(
			snapshot.AcademicYear.Id,
			kind,
			code,
			routeDefinitionIds);
		Add(
			"Applicant records",
			applicantReferences.Count,
			"Current-year applicants whose stage or immutable route snapshot references this code.",
			applicantReferences.SampleIds);

		return families;
	}

	private static void AddPlaybookReferenceFamily(
		ICollection<AdmissionCrmConfigurationReferenceFamilyDto> families,
		string family,
		string description,
		IEnumerable<AdmissionCrmPlaybook> playbooks,
		Func<AdmissionCrmPlaybook, string> jsonSelector,
		string code)
	{
		var matches = playbooks
			.Select(x => new { x.Id, Count = CountMatches(code, jsonSelector(x)) })
			.Where(x => x.Count > 0)
			.ToList();
		families.Add(new AdmissionCrmConfigurationReferenceFamilyDto
		{
			Family = family,
			ReferenceCount = matches.Sum(x => x.Count),
			Description = description,
			SampleReferenceIds = matches.Select(x => x.Id).OrderBy(x => x).Take(25).ToList()
		});
	}

	private static IReadOnlyList<string> GetChangedFields(
		AdmissionCrmCycleConfiguration? current,
		UpdateAdmissionCrmCycleConfigurationDto? proposed)
	{
		if (proposed == null)
		{
			return [];
		}
		if (current == null)
		{
			return ["CycleConfiguration"];
		}

		var changed = new List<string>();
		AddIfChanged(changed, "CycleName", current.CycleName, proposed.CycleName);
		AddIfChanged(changed, "IsActive", current.IsActive, proposed.IsActive);
		AddIfChanged(
			changed,
			"RegistrationFormRequiredBeforeExam",
			current.RegistrationFormRequiredBeforeExam,
			proposed.RegistrationFormRequiredBeforeExam);
		AddIfChanged(changed, "EntranceExamRequired", current.EntranceExamRequired, proposed.EntranceExamRequired);
		AddIfChanged(changed, "StaleAfterDays", current.StaleAfterDays, proposed.StaleAfterDays);
		AddIfChanged(changed, "ArchiveAfterDays", current.ArchiveAfterDays, proposed.ArchiveAfterDays);
		AddIfChanged(changed, "DefaultLeadSlaHours", current.DefaultLeadSlaHours, proposed.DefaultLeadSlaHours);
		AddIfChanged(
			changed,
			"ActivationCancellationWindowDays",
			current.ActivationCancellationWindowDays,
			proposed.ActivationCancellationWindowDays);
		AddIfChanged(
			changed,
			"DefaultExamPassingPercentage",
			current.DefaultExamPassingPercentage,
			proposed.DefaultExamPassingPercentage);
		AddIfChanged(changed, "MeritRankingMode", current.MeritRankingMode, proposed.MeritRankingMode);
		AddIfChanged(changed, "ExamReadinessFields", current.ExamReadinessFields, proposed.ExamReadinessFields);
		AddIfChanged(
			changed,
			"LeadScoringRules",
			current.LeadScoringRulesJson,
			AdmissionCrmLeadScoreCalculator.SerializeRules(proposed.LeadScoringRules));
		AddIfChanged(
			changed,
			"LeadRoutingSettings",
			current.LeadRoutingRulesJson,
			AdmissionCrmLeadRoutingRuleMatcher.SerializeSettings(proposed.LeadRoutingSettings));
		AddIfChanged(
			changed,
			"JourneyRules",
			current.JourneyRulesJson,
			AdmissionCrmJourneyRuleSerializer.SerializeRules(proposed.JourneyRules));
		AddIfChanged(
			changed,
			"ApplicantChecklist",
			current.ApplicantChecklistJson,
			AdmissionCrmApplicantChecklistSerializer.SerializeItems(proposed.ApplicantChecklist));
		AddIfChanged(
			changed,
			"ChannelSettings",
			current.ChannelSettingsJson,
			AdmissionCrmChannelSettingsSerializer.SerializeSettings(proposed.ChannelSettings));
		AddIfChanged(
			changed,
			"SlaPolicy",
			current.SlaPolicyJson,
			JsonSerializer.Serialize(proposed.SlaPolicy, JsonOptions));
		AddIfChanged(changed, "Notes", current.Notes ?? string.Empty, proposed.Notes ?? string.Empty);
		return changed;
	}

	private static IReadOnlyList<string> GetChangedFields(
		AdmissionCrmPipelineStage? current,
		UpdateAdmissionCrmPipelineStageDto? proposed)
	{
		if (proposed == null)
		{
			return [];
		}
		if (current == null)
		{
			return ["PipelineStage"];
		}

		var changed = new List<string>();
		AddIfChanged(changed, "StageName", current.StageName, proposed.StageName);
		AddIfChanged(changed, "DisplayOrder", current.DisplayOrder, proposed.DisplayOrder);
		AddIfChanged(changed, "IsEnabled", current.IsEnabled, proposed.IsEnabled);
		AddIfChanged(changed, "ColorHex", current.ColorHex, proposed.ColorHex ?? current.ColorHex);
		AddIfChanged(
			changed,
			"SlaTargetHours",
			current.SlaTargetHours,
			proposed.SlaTargetHours ?? current.SlaTargetHours);
		AddIfChanged(
			changed,
			"OwnerResponsibility",
			current.OwnerResponsibility,
			proposed.OwnerResponsibility ?? current.OwnerResponsibility);
		if (proposed.AllowedNextStages != null)
		{
			AddIfChanged(
				changed,
				"AllowedNextStages",
				current.AllowedNextStagesJson,
				JsonSerializer.Serialize(proposed.AllowedNextStages, JsonOptions));
		}
		return changed;
	}

	private static void ValidateImpactInput(PreviewAdmissionCrmConfigurationImpactDto input)
	{
		if (input.Kind.HasValue != !string.IsNullOrWhiteSpace(input.Code))
		{
			throw new BusinessException("AdmissionCrm:ConfigurationReferenceKindAndCodeRequiredTogether");
		}
		if (input.PipelineStageId.HasValue != (input.ProposedPipelineStage != null))
		{
			throw new BusinessException("AdmissionCrm:ConfigurationStageIdAndProposalRequiredTogether");
		}
		if (input.PipelineStageId == Guid.Empty)
		{
			throw new BusinessException("AdmissionCrm:PipelineStageRequired");
		}
		if (input.PipelineStageId.HasValue && input.Kind.HasValue)
		{
			throw new BusinessException("AdmissionCrm:ConfigurationStageReferenceIsDerived");
		}
		if (!input.Kind.HasValue &&
		    input.ProposedCycleConfiguration == null &&
		    !input.PipelineStageId.HasValue)
		{
			throw new BusinessException("AdmissionCrm:ConfigurationImpactInputRequired");
		}
	}

	private static object BuildImpactInputSignature(
		AdmissionCrmConfigurationReferenceKind? kind,
		string? code,
		UpdateAdmissionCrmCycleConfigurationDto? proposedCycle,
		Guid? pipelineStageId,
		UpdateAdmissionCrmPipelineStageDto? proposedStage) => new
	{
		Kind = kind,
		Code = kind.HasValue ? NormalizeCode(code!) : null,
		ProposedCycle = proposedCycle == null
			? null
			: new
			{
				proposedCycle.CycleName,
				proposedCycle.IsActive,
				proposedCycle.RegistrationFormRequiredBeforeExam,
				proposedCycle.EntranceExamRequired,
				proposedCycle.StaleAfterDays,
				proposedCycle.ArchiveAfterDays,
				proposedCycle.DefaultLeadSlaHours,
				proposedCycle.SlaPolicy,
				proposedCycle.ActivationCancellationWindowDays,
				proposedCycle.DefaultExamPassingPercentage,
				proposedCycle.MeritRankingMode,
				proposedCycle.ExamReadinessFields,
				proposedCycle.LeadScoringRules,
				proposedCycle.LeadRoutingSettings,
				proposedCycle.JourneyRules,
				proposedCycle.ApplicantChecklist,
				proposedCycle.ChannelSettings,
				proposedCycle.Notes
			},
		PipelineStageId = pipelineStageId,
		ProposedStage = proposedStage == null
			? null
			: new
			{
				proposedStage.StageName,
				proposedStage.DisplayOrder,
				proposedStage.IsEnabled,
				proposedStage.ColorHex,
				proposedStage.SlaTargetHours,
				proposedStage.OwnerResponsibility,
				proposedStage.AllowedNextStages
			}
	};

	private static void AddIfChanged<T>(ICollection<string> changed, string field, T current, T proposed)
	{
		if (!EqualityComparer<T>.Default.Equals(current, proposed))
		{
			changed.Add(field);
		}
	}

	private static IReadOnlyList<string> BuildImpactWarnings(
		int applicantCount,
		int referenceCount,
		IReadOnlyCollection<string> changedFields,
		AdmissionCrmConfigurationReferenceKind? kind,
		string? code)
	{
		var warnings = new List<string>();
		if (changedFields.Count > 0 && applicantCount > 0)
		{
			warnings.Add(
				$"{applicantCount} current-year applicant record(s) may be affected by the proposed cycle configuration.");
		}
		if (changedFields.Contains("EntranceExamRequired"))
		{
			warnings.Add("Changing entrance-exam requirements can alter registration gates and applicant actions.");
		}
		if (changedFields.Contains("JourneyRules") ||
		    changedFields.Contains("LeadRoutingSettings") ||
		    changedFields.Contains("ChannelSettings"))
		{
			warnings.Add("Journey, routing, or channel changes can alter future ownership and communications.");
		}
		if (kind.HasValue && referenceCount > 0)
		{
			warnings.Add(
				$"{referenceCount} reference(s) to {kind.Value} code '{NormalizeCode(code!)}' must be reviewed before changing or retiring it.");
		}
		if (kind.HasValue && referenceCount == 0)
		{
			warnings.Add($"No current-year references were found for {kind.Value} code '{NormalizeCode(code!)}'.");
		}
		return warnings;
	}

	private static AdmissionCrmHierarchyMapping BuildHierarchyMapping(
		AdmissionCrmConfigurationSnapshot source,
		AdmissionCrmConfigurationSnapshot target)
	{
		var idMap = new Dictionary<Guid, Guid>();
		foreach (var sourceBoard in source.EducationBoards.OrderBy(x => x.Id))
		{
			var targetBoard = target.EducationBoards
				.OrderBy(x => x.Id)
				.FirstOrDefault(x => x.Type == sourceBoard.Type);
			if (targetBoard != null)
			{
				idMap[sourceBoard.Id] = targetBoard.Id;
			}
		}
		foreach (var sourceMedium in source.EducationMediums.OrderBy(x => x.Id))
		{
			if (!idMap.TryGetValue(sourceMedium.EducationBoardId, out var targetBoardId))
			{
				continue;
			}
			var targetMedium = target.EducationMediums
				.OrderBy(x => x.Id)
				.FirstOrDefault(x =>
					x.EducationBoardId == targetBoardId &&
					NamesMatch(x.Name, sourceMedium.Name));
			if (targetMedium != null)
			{
				idMap[sourceMedium.Id] = targetMedium.Id;
			}
		}
		foreach (var sourceStage in source.EducationMediumStages.OrderBy(x => x.Id))
		{
			if (!idMap.TryGetValue(sourceStage.EducationMediumId, out var targetMediumId))
			{
				continue;
			}
			var targetStage = target.EducationMediumStages
				.OrderBy(x => x.Id)
				.FirstOrDefault(x =>
					x.EducationMediumId == targetMediumId &&
					NamesMatch(x.Name, sourceStage.Name));
			if (targetStage != null)
			{
				idMap[sourceStage.Id] = targetStage.Id;
			}
		}
		foreach (var sourceStandard in source.Standards.OrderBy(x => x.Id))
		{
			if (!idMap.TryGetValue(sourceStandard.EducationMediumStageId, out var targetStageId))
			{
				continue;
			}
			var targetStandard = target.Standards
				.OrderBy(x => x.Id)
				.FirstOrDefault(x =>
					x.EducationMediumStageId == targetStageId &&
					NamesMatch(x.Name, sourceStandard.Name));
			if (targetStandard != null)
			{
				idMap[sourceStandard.Id] = targetStandard.Id;
			}
		}
		foreach (var sourceDivision in source.Divisions.OrderBy(x => x.Id))
		{
			if (!idMap.TryGetValue(sourceDivision.StandardId, out var targetStandardId))
			{
				continue;
			}
			var targetDivision = target.Divisions
				.OrderBy(x => x.Id)
				.FirstOrDefault(x =>
					x.StandardId == targetStandardId &&
					NamesMatch(x.Name, sourceDivision.Name));
			if (targetDivision != null)
			{
				idMap[sourceDivision.Id] = targetDivision.Id;
			}
		}

		var hierarchyKinds = source.EducationBoards.ToDictionary(x => x.Id, _ => "education board");
		foreach (var item in source.EducationMediums) hierarchyKinds[item.Id] = "academic medium";
		foreach (var item in source.EducationMediumStages) hierarchyKinds[item.Id] = "academic medium stage";
		foreach (var item in source.Standards) hierarchyKinds[item.Id] = "standard";
		foreach (var item in source.Divisions) hierarchyKinds[item.Id] = "division";

		var referencedSourceIds = GetConfigurationJson(source)
			.SelectMany(GetGuidTokens)
			.Where(hierarchyKinds.ContainsKey)
			.ToHashSet();
		var conflicts = new HashSet<string>(StringComparer.Ordinal);
		var sourceBoardIds = source.EducationBoards.Select(x => x.Id).ToHashSet();
		var sourceMediumIds = source.EducationMediums.Select(x => x.Id).ToHashSet();
		var sourceStandardIds = source.Standards.Select(x => x.Id).ToHashSet();
		foreach (var series in source.NumberSeries.OrderBy(x => x.Id))
		{
			AddDirectHierarchyReference(
				series.EducationBoardId,
				"education board",
				series.Code,
				sourceBoardIds,
				referencedSourceIds,
				conflicts);
			AddDirectHierarchyReference(
				series.AcademicMediumId,
				"academic medium",
				series.Code,
				sourceMediumIds,
				referencedSourceIds,
				conflicts);
			AddDirectHierarchyReference(
				series.StandardId,
				"standard",
				series.Code,
				sourceStandardIds,
				referencedSourceIds,
				conflicts);
		}

		foreach (var sourceId in referencedSourceIds.OrderBy(x => x))
		{
			if (hierarchyKinds.TryGetValue(sourceId, out var hierarchyKind) &&
			    !idMap.ContainsKey(sourceId))
			{
				conflicts.Add(
					$"Referenced source-year {hierarchyKind} '{sourceId:D}' has no equivalent record in the target academic year.");
			}
		}

		return new AdmissionCrmHierarchyMapping(
			idMap,
			referencedSourceIds,
			conflicts.OrderBy(x => x, StringComparer.Ordinal).ToList());
	}

	private static void AddDirectHierarchyReference(
		Guid? sourceId,
		string hierarchyKind,
		string seriesCode,
		IReadOnlySet<Guid> validSourceIds,
		ISet<Guid> referencedSourceIds,
		ISet<string> conflicts)
	{
		if (!sourceId.HasValue)
		{
			return;
		}
		referencedSourceIds.Add(sourceId.Value);
		if (!validSourceIds.Contains(sourceId.Value))
		{
			conflicts.Add(
				$"Identifier number series '{seriesCode}' references {hierarchyKind} '{sourceId.Value:D}', which does not belong to the source academic year.");
		}
	}

	private static bool CanMapNumberSeries(
		AdmissionCrmNumberSeries series,
		AdmissionCrmConfigurationSnapshot source,
		IReadOnlyDictionary<Guid, Guid> idMap)
	{
		return CanMapOptional(series.EducationBoardId, source.EducationBoards.Select(x => x.Id), idMap) &&
		       CanMapOptional(series.AcademicMediumId, source.EducationMediums.Select(x => x.Id), idMap) &&
		       CanMapOptional(series.StandardId, source.Standards.Select(x => x.Id), idMap);
	}

	private static bool CanMapOptional(
		Guid? sourceId,
		IEnumerable<Guid> validSourceIds,
		IReadOnlyDictionary<Guid, Guid> idMap)
	{
		return !sourceId.HasValue ||
		       (validSourceIds.Contains(sourceId.Value) && idMap.ContainsKey(sourceId.Value));
	}

	private static IEnumerable<Guid> GetGuidTokens(string value)
	{
		foreach (Match match in GuidPattern.Matches(value))
		{
			if (Guid.TryParse(match.Value, out var id))
			{
				yield return id;
			}
		}
	}

	private static bool NamesMatch(string left, string right) =>
		string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

	private static IReadOnlyList<AdmissionCrmConfigurationCloneFamilyDto> BuildCloneFamilies(
		AdmissionCrmConfigurationSnapshot source,
		AdmissionCrmConfigurationSnapshot target,
		AdmissionCrmHierarchyMapping hierarchyMapping)
	{
		var cloneableNumberSeries = source.NumberSeries.Count(x =>
			CanMapNumberSeries(x, source, hierarchyMapping.IdMap));
		return
		[
			Family("Cycle configuration", source.Cycle == null ? 0 : 1, source.Cycle == null ? 0 : 1,
				target.Cycle == null ? 0 : 1, "Settings are copied unlocked with known configuration IDs remapped."),
			Family("Pipeline stages", source.PipelineStages.Count, source.PipelineStages.Count,
				target.PipelineStages.Count, "System and custom stage definitions are copied."),
			Family("Inquiry sources", source.InquirySources.Count, source.InquirySources.Count,
				target.InquirySources.Count, "Codes, allowed channels, routing eligibility, and active state are copied."),
			Family("Inquiry forms", source.InquiryForms.Count, source.InquiryForms.Count,
				target.InquiryForms.Count, "Fields, localizations, privacy metadata, and active state are copied with new IDs."),
			Family("Message templates", source.MessageTemplates.Count, source.MessageTemplates.Count,
				target.MessageTemplates.Count, "Templates are recreated as drafts and known IDs are remapped."),
			Family("Admission playbooks", source.Playbooks.Count, source.Playbooks.Count,
				target.Playbooks.Count, "Journey, round, exam, seat-plan, slot, and assignment configuration is recreated as drafts."),
			Family("Registration templates", source.RegistrationTemplates.Count, source.RegistrationTemplates.Count,
				target.RegistrationTemplates.Count, "Registration templates are recreated as drafts."),
			Family("Registration form number series", source.FormNumberSeries.Count, source.FormNumberSeries.Count,
				target.FormNumberSeries.Count, "Patterns are copied and counters restart at one."),
			Family("Document checklist templates", source.DocumentChecklists.Count, source.DocumentChecklists.Count,
				target.DocumentChecklists.Count, "Document checklists are recreated as drafts."),
			Family("Seat pools", source.SeatPools.Count, source.SeatPools.Count,
				target.SeatPools.Count, "Definitions and capacities are copied; applicant and occupancy counters reset."),
			Family("Fee policies", source.FeePolicies.Count, source.FeePolicies.Count,
				target.FeePolicies.Count, "Fee policies are recreated as drafts with target-year-shifted effective dates."),
			Family("Route definitions", source.RouteDefinitions.Count, source.RouteDefinitions.Count,
				target.RouteDefinitions.Count, "Route definitions are recreated as drafts with target-year-shifted effective dates."),
			Family("Policy adoptions", source.PolicyAdoptions.Count, source.PolicyAdoptions.Count,
				target.PolicyAdoptions.Count, "Year-bound adoptions are recreated by the confirming user; global policy packs are reused."),
			Family("Identifier number series", source.NumberSeries.Count, cloneableNumberSeries,
				target.NumberSeries.Count, "Series are recreated as drafts, hierarchy IDs are mapped to the target year, and counters reset."),
			Family("Global policy packs", source.PolicyPacks.Count, 0,
				target.PolicyPacks.Count, "Shared policy packs are referenced and are never duplicated.")
		];
	}

	private static AdmissionCrmConfigurationCloneFamilyDto Family(
		string family,
		int sourceCount,
		int cloneableCount,
		int targetCount,
		string cloneBehavior) =>
		new()
		{
			Family = family,
			SourceCount = sourceCount,
			CloneableCount = cloneableCount,
			TargetCount = targetCount,
			CloneBehavior = cloneBehavior
		};

	private static IReadOnlyList<string> BuildCloneWarnings(
		AdmissionCrmConfigurationSnapshot source,
		AdmissionCrmHierarchyMapping hierarchyMapping)
	{
		var warnings = new List<string>
		{
			"Journey locks and approval history are not copied; approval-controlled playbooks, templates, routes, fee policies, and number series are recreated as drafts.",
			"Known inquiry-source, inquiry-form, message-template, playbook, route, and configuration IDs are remapped inside cloned JSON.",
			"Registration-form counters, identifier counters, and seat-pool applicant or occupancy counters are reset for the target year.",
			"Effective dates are shifted by the difference between source and target academic-year start dates.",
			"Global policy packs are reused rather than duplicated; year-bound policy adoptions are recreated by the confirming user."
		};
		if (hierarchyMapping.ReferencedSourceIds.Count > 0 &&
		    hierarchyMapping.Conflicts.Count == 0)
		{
			warnings.Add(
				$"{hierarchyMapping.ReferencedSourceIds.Count} source-year academic hierarchy identifier(s) will be mapped to equivalent target-year records.");
		}
		if (ContainsUnmappedGuidValues(source))
		{
			warnings.Add(
				"Staff, campus, or other non-hierarchy identifiers that are not known admission configuration IDs are copied verbatim; review owner and external-reference mappings before publishing.");
		}
		return warnings;
	}

	private static bool ContainsUnmappedGuidValues(AdmissionCrmConfigurationSnapshot snapshot)
	{
		var knownIds = new HashSet<Guid>();
		if (snapshot.Cycle != null) knownIds.Add(snapshot.Cycle.Id);
		foreach (var id in snapshot.PipelineStages.Select(x => x.Id)
			         .Concat(snapshot.InquirySources.Select(x => x.Id))
			         .Concat(snapshot.InquiryForms.Select(x => x.Id))
			         .Concat(snapshot.MessageTemplates.Select(x => x.Id))
			         .Concat(snapshot.Playbooks.Select(x => x.Id))
			         .Concat(snapshot.RegistrationTemplates.Select(x => x.Id))
			         .Concat(snapshot.FormNumberSeries.Select(x => x.Id))
			         .Concat(snapshot.DocumentChecklists.Select(x => x.Id))
			         .Concat(snapshot.SeatPools.Select(x => x.Id))
			         .Concat(snapshot.FeePolicies.Select(x => x.Id))
			         .Concat(snapshot.RouteDefinitions.Select(x => x.Id))
			         .Concat(snapshot.PolicyAdoptions.Select(x => x.Id))
			         .Concat(snapshot.NumberSeries.Select(x => x.Id)))
		{
			knownIds.Add(id);
		}
		foreach (var id in snapshot.EducationBoards.Select(x => x.Id)
			         .Concat(snapshot.EducationMediums.Select(x => x.Id))
			         .Concat(snapshot.EducationMediumStages.Select(x => x.Id))
			         .Concat(snapshot.Standards.Select(x => x.Id))
			         .Concat(snapshot.Divisions.Select(x => x.Id)))
		{
			knownIds.Add(id);
		}

		return GetConfigurationJson(snapshot)
			.SelectMany(GetGuidValues)
			.Any(id => !knownIds.Contains(id));
	}

	private static IEnumerable<string> GetConfigurationJson(AdmissionCrmConfigurationSnapshot snapshot)
	{
		if (snapshot.Cycle != null)
		{
			yield return snapshot.Cycle.LeadScoringRulesJson;
			yield return snapshot.Cycle.LeadRoutingRulesJson;
			yield return snapshot.Cycle.JourneyRulesJson;
			yield return snapshot.Cycle.ApplicantChecklistJson;
			yield return snapshot.Cycle.ChannelSettingsJson;
			yield return snapshot.Cycle.SlaPolicyJson;
		}
		foreach (var x in snapshot.InquiryForms)
		{
			yield return x.FieldsJson;
			yield return x.LocalizationsJson;
		}
		foreach (var x in snapshot.MessageTemplates)
		{
			yield return x.Body;
			yield return x.VariablesJson;
		}
		foreach (var x in snapshot.Playbooks)
		{
			yield return x.ApplicabilityJson;
			yield return x.StagesJson;
			yield return x.RoundsJson;
			yield return x.SeatPlansJson;
			yield return x.ExamDefinitionsJson;
			yield return x.SlotTemplatesJson;
			yield return x.AssignmentRulesJson;
		}
		foreach (var x in snapshot.RegistrationTemplates)
		{
			yield return x.ApplicabilityJson;
			yield return x.SectionsJson;
		}
		foreach (var x in snapshot.FormNumberSeries) yield return x.ApplicabilityJson;
		foreach (var x in snapshot.DocumentChecklists)
		{
			yield return x.ApplicabilityJson;
			yield return x.RequirementsJson;
		}
		foreach (var x in snapshot.FeePolicies)
		{
			yield return x.ApplicabilityJson;
			yield return x.ComponentsJson;
		}
		foreach (var x in snapshot.RouteDefinitions) yield return x.ConfigurationJson;
		foreach (var x in snapshot.PolicyAdoptions)
		{
			yield return x.ApplicabilityJson;
			yield return x.OverridesJson;
		}
	}

	private static IEnumerable<Guid> GetGuidValues(string json)
	{
		JsonDocument document;
		try
		{
			document = JsonDocument.Parse(json);
		}
		catch (JsonException)
		{
			yield break;
		}
		using (document)
		{
			foreach (var value in GetGuidValues(document.RootElement))
			{
				yield return value;
			}
		}
	}

	private static IEnumerable<Guid> GetGuidValues(JsonElement element)
	{
		switch (element.ValueKind)
		{
			case JsonValueKind.Array:
				foreach (var item in element.EnumerateArray())
				foreach (var value in GetGuidValues(item))
					yield return value;
				break;
			case JsonValueKind.Object:
				foreach (var property in element.EnumerateObject())
				foreach (var value in GetGuidValues(property.Value))
					yield return value;
				break;
			case JsonValueKind.String:
				if (Guid.TryParse(element.GetString(), out var id))
				{
					yield return id;
				}
				break;
		}
	}

	private static int CountMatches(string code, params string?[] values) =>
		values.Where(x => !string.IsNullOrWhiteSpace(x)).Sum(x => CountJsonMatches(x!, code));

	private static int CountJsonMatches(string json, string code)
	{
		try
		{
			using var document = JsonDocument.Parse(json);
			return CountJsonMatches(document.RootElement, code);
		}
		catch (JsonException)
		{
			return CountExactText(code, json);
		}
	}

	private static int CountJsonMatches(JsonElement element, string code)
	{
		return element.ValueKind switch
		{
			JsonValueKind.Array => element.EnumerateArray().Sum(x => CountJsonMatches(x, code)),
			JsonValueKind.Object => element.EnumerateObject().Sum(x => CountJsonMatches(x.Value, code)),
			JsonValueKind.String => string.Equals(
				element.GetString(),
				code,
				StringComparison.OrdinalIgnoreCase) ? 1 : 0,
			_ => 0
		};
	}

	private static int CountExactText(string code, string? value) =>
		string.Equals(value?.Trim(), code, StringComparison.OrdinalIgnoreCase) ? 1 : 0;

	private static object BuildSnapshotSignature(AdmissionCrmConfigurationSnapshot snapshot) => new
	{
		Year = new
		{
			snapshot.AcademicYear.Id,
			snapshot.AcademicYear.StartDate,
			snapshot.AcademicYear.EndDate
		},
		snapshot.Cycle,
		PipelineStages = snapshot.PipelineStages.OrderBy(x => x.Id),
		InquirySources = snapshot.InquirySources.OrderBy(x => x.Id),
		InquiryForms = snapshot.InquiryForms.OrderBy(x => x.Id),
		MessageTemplates = snapshot.MessageTemplates.OrderBy(x => x.Id),
		Playbooks = snapshot.Playbooks.OrderBy(x => x.Id),
		RegistrationTemplates = snapshot.RegistrationTemplates.OrderBy(x => x.Id),
		FormNumberSeries = snapshot.FormNumberSeries.OrderBy(x => x.Id),
		DocumentChecklists = snapshot.DocumentChecklists.OrderBy(x => x.Id),
		SeatPools = snapshot.SeatPools.OrderBy(x => x.Id),
		FeePolicies = snapshot.FeePolicies.OrderBy(x => x.Id),
		RouteDefinitions = snapshot.RouteDefinitions.OrderBy(x => x.Id),
		PolicyAdoptions = snapshot.PolicyAdoptions.OrderBy(x => x.Id),
		NumberSeries = snapshot.NumberSeries.OrderBy(x => x.Id),
		PolicyPacks = snapshot.PolicyPacks.OrderBy(x => x.Id),
		EducationBoards = snapshot.EducationBoards
			.OrderBy(x => x.Id)
			.Select(x => new { x.Id, x.Type, x.StartDate, x.EndDate }),
		EducationMediums = snapshot.EducationMediums
			.OrderBy(x => x.Id)
			.Select(x => new { x.Id, x.EducationBoardId, x.Name }),
		EducationMediumStages = snapshot.EducationMediumStages
			.OrderBy(x => x.Id)
			.Select(x => new { x.Id, x.EducationMediumId, x.Name }),
		Standards = snapshot.Standards
			.OrderBy(x => x.Id)
			.Select(x => new { x.Id, x.EducationMediumStageId, x.Name }),
		Divisions = snapshot.Divisions
			.OrderBy(x => x.Id)
			.Select(x => new { x.Id, x.StandardId, x.Name })
	};

	private static bool TryGetCloneMetadata(
		AdmissionCrmCycleConfiguration? targetCycle,
		out Guid sourceAcademicYearId,
		out string fingerprint)
	{
		sourceAcademicYearId = Guid.Empty;
		fingerprint = string.Empty;
		if (targetCycle == null ||
		    !targetCycle.ExtraProperties.TryGetValue(
			    AdmissionCrmConfigurationCloneWriter.CloneSourceAcademicYearProperty,
			    out var sourceValue) ||
		    !targetCycle.ExtraProperties.TryGetValue(
			    AdmissionCrmConfigurationCloneWriter.ClonePreviewFingerprintProperty,
			    out var fingerprintValue))
		{
			return false;
		}

		var sourceText = GetExtraPropertyText(sourceValue);
		fingerprint = GetExtraPropertyText(fingerprintValue);
		return Guid.TryParse(sourceText, out sourceAcademicYearId) &&
		       !string.IsNullOrWhiteSpace(fingerprint);
	}

	private static string GetExtraPropertyText(object? value)
	{
		return value switch
		{
			null => string.Empty,
			JsonElement { ValueKind: JsonValueKind.String } element => element.GetString() ?? string.Empty,
			_ => Convert.ToString(value) ?? string.Empty
		};
	}

	private static void ValidateCloneYearPair(Guid sourceAcademicYearId, Guid targetAcademicYearId)
	{
		if (sourceAcademicYearId == Guid.Empty || targetAcademicYearId == Guid.Empty)
		{
			throw new BusinessException("AdmissionCrm:AcademicYearRequired");
		}
		if (sourceAcademicYearId == targetAcademicYearId)
		{
			throw new BusinessException("AdmissionCrm:ConfigurationCloneDistinctYearsRequired");
		}
	}

	private static string GetCloneConflictErrorCode(AdmissionCrmConfigurationClonePreviewDto preview)
	{
		return preview.Conflicts.Any(x =>
			x.Contains("source-year", StringComparison.OrdinalIgnoreCase) ||
			x.Contains("does not belong to the source academic year", StringComparison.OrdinalIgnoreCase))
			? "AdmissionCrm:ConfigurationCloneHierarchyMappingRequired"
			: "AdmissionCrm:ConfigurationCloneTargetNotEmpty";
	}

	private static string NormalizeCode(string value) =>
		Check.NotNullOrWhiteSpace(value, nameof(value), AdmissionCrmConsts.MaxPolicyCodeLength)
			.Trim()
			.ToUpperInvariant();

	private static string ComputeFingerprint(string value) =>
		Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

	private sealed record AdmissionCrmHierarchyMapping(
		IReadOnlyDictionary<Guid, Guid> IdMap,
		IReadOnlySet<Guid> ReferencedSourceIds,
		IReadOnlyList<string> Conflicts);
}

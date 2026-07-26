using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmPolicyPackAppService : ApplicationService, IAdmissionCrmPolicyPackAppService
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private static readonly DateTime OfficialCatalogVerifiedAt = new(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc);
	private readonly IRepository<AdmissionCrmPolicyPack, Guid> _packRepository;
	private readonly IRepository<AdmissionCrmPolicyAdoption, Guid> _adoptionRepository;
	private readonly IRepository<AdmissionCrmPolicyEvaluation, Guid> _evaluationRepository;
	private readonly IRepository<AdmissionCrmPolicyException, Guid> _exceptionRepository;
	private readonly IRepository<AdmissionInquiryLead, Guid> _leadRepository;
	private readonly IRepository<AdmissionCrmTimelineEvent, Guid> _timelineRepository;
	private readonly StudentScopeAccessService _studentScopeAccessService;

	public AdmissionCrmPolicyPackAppService(
		IRepository<AdmissionCrmPolicyPack, Guid> packRepository,
		IRepository<AdmissionCrmPolicyAdoption, Guid> adoptionRepository,
		IRepository<AdmissionCrmPolicyEvaluation, Guid> evaluationRepository,
		IRepository<AdmissionCrmPolicyException, Guid> exceptionRepository,
		IRepository<AdmissionInquiryLead, Guid> leadRepository,
		IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository,
		StudentScopeAccessService studentScopeAccessService)
	{
		_packRepository = packRepository;
		_adoptionRepository = adoptionRepository;
		_evaluationRepository = evaluationRepository;
		_exceptionRepository = exceptionRepository;
		_leadRepository = leadRepository;
		_timelineRepository = timelineRepository;
		_studentScopeAccessService = studentScopeAccessService;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManagePolicyPacks)]
	public async Task<AdmissionCrmPolicyPackDto> CreatePolicyPackAsync(CreateAdmissionCrmPolicyPackDto input)
	{
		ValidatePack(input);
		var queryable = await _packRepository.GetQueryableAsync();
		var existing = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.Code == input.Code.ToUpper()));
		var version = existing.Count == 0 ? 1 : existing.Max(x => x.Version) + 1;
		var pack = new AdmissionCrmPolicyPack(GuidGenerator.Create(), CurrentTenant.Id, input.Code,
			input.Name, input.Jurisdiction, input.Board, version, Serialize(input.Sources), Serialize(input.Rules),
			input.EffectiveFrom, input.EffectiveUntil, input.ReviewDueAt);
		await _packRepository.InsertAsync(pack, autoSave: true);
		return MapPack(pack);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewPolicyPacks)]
	public async Task<PagedResultDto<AdmissionCrmPolicyPackDto>> GetPolicyPacksAsync(GetAdmissionCrmPolicyPackListInput input)
	{
		var queryable = await _packRepository.GetQueryableAsync();
		var entities = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id));
		var filtered = entities
			.Where(x => !input.Status.HasValue || x.Status == input.Status)
			.Where(x => string.IsNullOrWhiteSpace(input.Board) || x.Board.Equals(input.Board.Trim(), StringComparison.OrdinalIgnoreCase))
			.Where(x => string.IsNullOrWhiteSpace(input.Jurisdiction) || x.Jurisdiction.Equals(input.Jurisdiction.Trim(), StringComparison.OrdinalIgnoreCase))
			.OrderBy(x => x.Code).ThenByDescending(x => x.Version).ToList();
		return new PagedResultDto<AdmissionCrmPolicyPackDto>(filtered.Count,
			filtered.Skip(input.SkipCount).Take(input.MaxResultCount).Select(MapPack).ToList());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManagePolicyPacks)]
	public async Task<AdmissionCrmPolicyBaselineInstallResultDto> InstallOfficialBaselineDraftsAsync()
	{
		var queryable = await _packRepository.GetQueryableAsync();
		var existing = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id));
		var created = new List<AdmissionCrmPolicyPackDto>();
		var current = new List<string>();
		foreach (var baseline in BuildOfficialBaselines())
		{
			var matching = existing.Where(x => x.Code == baseline.Code).OrderByDescending(x => x.Version).FirstOrDefault();
			if (matching != null && IsCurrentOfficialBaseline(matching, baseline))
			{
				current.Add(baseline.Code);
				continue;
			}

			ValidatePack(baseline);
			var version = matching == null ? 1 : existing.Where(x => x.Code == baseline.Code).Max(x => x.Version) + 1;
			var pack = new AdmissionCrmPolicyPack(GuidGenerator.Create(), CurrentTenant.Id, baseline.Code,
				baseline.Name, baseline.Jurisdiction, baseline.Board, version, Serialize(baseline.Sources),
				Serialize(baseline.Rules), baseline.EffectiveFrom, baseline.EffectiveUntil, baseline.ReviewDueAt);
			await _packRepository.InsertAsync(pack, autoSave: true);
			existing.Add(pack);
			created.Add(MapPack(pack));
		}
		return new AdmissionCrmPolicyBaselineInstallResultDto
		{
			CatalogVerifiedAt = OfficialCatalogVerifiedAt,
			CreatedDrafts = created,
			AlreadyCurrentCodes = current
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManagePolicyPacks)]
	public async Task<AdmissionCrmPolicyPackDto> SubmitPolicyPackAsync(Guid id)
	{
		var pack = await GetPackAsync(id);
		pack.Submit(GetCurrentUserId(), Clock.Now);
		await _packRepository.UpdateAsync(pack, autoSave: true);
		return MapPack(pack);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ApprovePolicyPacks)]
	public async Task<AdmissionCrmPolicyPackDto> ApprovePolicyPackAsync(Guid id)
	{
		var pack = await GetPackAsync(id);
		pack.Approve(GetCurrentUserId(), Clock.Now);
		await _packRepository.UpdateAsync(pack, autoSave: true);
		return MapPack(pack);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ApprovePolicyPacks)]
	public async Task<AdmissionCrmPolicyPackDto> PublishPolicyPackAsync(Guid id)
	{
		var pack = await GetPackAsync(id);
		pack.Publish(Clock.Now);
		var queryable = await _packRepository.GetQueryableAsync();
		var prior = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.Code == pack.Code && x.Id != pack.Id && x.Status == AdmissionCrmPolicyPackStatus.Published));
		foreach (var previous in prior)
		{
			previous.Supersede(pack.Id);
			await _packRepository.UpdateAsync(previous);
		}
		await _packRepository.UpdateAsync(pack, autoSave: true);
		return MapPack(pack);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.AdoptPolicyPacks)]
	public async Task<AdmissionCrmPolicyAdoptionDto> AdoptPolicyPackAsync(
		Guid academicYearId, Guid policyPackId, AdoptAdmissionCrmPolicyPackDto input)
	{
		await _studentScopeAccessService.EnsureAcademicYearWideConfigurationAccessAsync(
			academicYearId,
			nameof(AdoptPolicyPackAsync));
		var pack = await GetPackAsync(policyPackId);
		if (pack.Status != AdmissionCrmPolicyPackStatus.Published)
			throw new BusinessException("AdmissionCrm:PublishedPolicyPackRequired");
		if (pack.ReviewDueAt < Clock.Now && string.IsNullOrWhiteSpace(input.OverrideReason))
			throw new BusinessException("AdmissionCrm:OverduePolicySourceReviewRequiresReason");
		var rules = Deserialize<IReadOnlyList<AdmissionCrmPolicyRuleDto>>(pack.RulesJson) ?? [];
		var unknownOverrides = input.Overrides.Where(x => rules.All(r => !r.Code.Equals(x.RuleCode, StringComparison.OrdinalIgnoreCase))).ToList();
		if (unknownOverrides.Count > 0)
			throw new BusinessException("AdmissionCrm:UnknownPolicyRuleOverride").WithData("RuleCode", unknownOverrides[0].RuleCode);
		var warnings = input.Overrides.Select(x => string.IsNullOrWhiteSpace(x.Warning)
			? $"School override differs from sourced rule {x.RuleCode}." : x.Warning).ToList();
		if (pack.ReviewDueAt < Clock.Now) warnings.Add("Official source review is overdue.");
		var adoption = new AdmissionCrmPolicyAdoption(GuidGenerator.Create(), CurrentTenant.Id,
			academicYearId, pack.Id, pack.Version, Serialize(input.Applicability), Serialize(input.Overrides),
			Serialize(new Dictionary<string, IReadOnlyList<string>> { ["warnings"] = warnings }), input.OverrideReason,
			input.EffectiveFrom, input.EffectiveUntil, GetCurrentUserId(), Clock.Now);
		await _adoptionRepository.InsertAsync(adoption, autoSave: true);
		return MapAdoption(adoption);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewPolicyPacks)]
	public async Task<IReadOnlyList<AdmissionCrmPolicyAdoptionDto>> GetAdoptionsAsync(Guid academicYearId)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(GetAdoptionsAsync));
		var queryable = await _adoptionRepository.GetQueryableAsync();
		var entities = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId));
		return entities.OrderByDescending(x => x.AdoptedAt).Select(MapAdoption).ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.EvaluatePolicy)]
	public async Task<AdmissionCrmPolicyEvaluationDto> EvaluateRuleAsync(Guid leadId, EvaluateAdmissionCrmPolicyRuleDto input)
	{
		var lead = await GetLeadAsync(leadId, nameof(EvaluateRuleAsync));
		var adoption = await GetAdoptionAsync(input.PolicyAdoptionId, lead.AcademicYearId);
		var pack = await GetPackAsync(adoption.PolicyPackId);
		var rules = Deserialize<IReadOnlyList<AdmissionCrmPolicyRuleDto>>(pack.RulesJson) ?? [];
		var rule = rules.SingleOrDefault(x => x.Code.Equals(input.RuleCode, StringComparison.OrdinalIgnoreCase)) ??
			throw new BusinessException("AdmissionCrm:PolicyRuleNotFound");
		var effectiveRule = ApplyOverride(rule, Deserialize<IReadOnlyList<AdmissionCrmPolicyOverrideDto>>(adoption.OverridesJson) ?? []);
		var evaluated = Evaluate(lead, effectiveRule, input);
		var snapshot = new Dictionary<string, object?>
		{
			["dateOfBirth"] = lead.DateOfBirth,
			["desiredClass"] = lead.DesiredClassName,
			["ageCutoffDate"] = effectiveRule.AgeCutoffDate,
			["minimumAgeYears"] = effectiveRule.MinimumAgeYears,
			["maximumAgeYears"] = effectiveRule.MaximumAgeYears,
			["policyPackId"] = pack.Id,
			["policyPackVersion"] = pack.Version
		};
		var entity = new AdmissionCrmPolicyEvaluation(GuidGenerator.Create(), CurrentTenant.Id,
			lead.AcademicYearId, lead.Id, adoption.Id, effectiveRule.Code, effectiveRule.Type,
			effectiveRule.Severity, evaluated.Status, Serialize(snapshot), Serialize(input.EvidenceReferences),
			evaluated.Explanation, GetCurrentUserId(), Clock.Now);
		await _evaluationRepository.InsertAsync(entity, autoSave: true);
		await AddTimelineAsync(lead, AdmissionCrmTimelineEventType.PolicyEvaluated,
			$"Policy evaluated: {effectiveRule.Name}", evaluated.Explanation, entity.Id);
		return MapEvaluation(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewPolicyPacks)]
	public async Task<AdmissionCrmPolicyReadinessDto> GetPolicyReadinessAsync(Guid leadId)
	{
		await GetLeadAsync(leadId, nameof(GetPolicyReadinessAsync));
		var evaluationQuery = await _evaluationRepository.GetQueryableAsync();
		var evaluations = await AsyncExecuter.ToListAsync(evaluationQuery.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == leadId));
		var latest = evaluations.GroupBy(x => new { x.PolicyAdoptionId, x.RuleCode })
			.Select(x => x.OrderByDescending(y => y.EvaluatedAt).First()).OrderBy(x => x.RuleCode).ToList();
		var exceptionQuery = await _exceptionRepository.GetQueryableAsync();
		var exceptions = await AsyncExecuter.ToListAsync(exceptionQuery.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == leadId));
		var blocking = latest.Count(x => x.Severity is AdmissionCrmPolicyRuleSeverity.Blocking or AdmissionCrmPolicyRuleSeverity.ApprovalRequired &&
			x.Status is AdmissionCrmPolicyEvaluationStatus.NotSatisfied or AdmissionCrmPolicyEvaluationStatus.ManualReviewRequired);
		var evidencePending = latest.Count(x => x.Status == AdmissionCrmPolicyEvaluationStatus.EvidencePending);
		return new AdmissionCrmPolicyReadinessDto
		{
			LeadId = leadId,
			Evaluations = latest.Select(MapEvaluation).ToList(),
			Exceptions = exceptions.OrderByDescending(x => x.RequestedAt).Select(MapException).ToList(),
			BlockingFailureCount = blocking,
			PendingEvidenceCount = evidencePending,
			IsPolicyReady = latest.Count > 0 && blocking == 0 && evidencePending == 0
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.RequestPolicyException)]
	public async Task<AdmissionCrmPolicyExceptionDto> RequestExceptionAsync(Guid evaluationId, RequestAdmissionCrmPolicyExceptionDto input)
	{
		var evaluation = await GetEvaluationAsync(evaluationId);
		var lead = await GetLeadAsync(evaluation.LeadId, nameof(RequestExceptionAsync));
		if (evaluation.Status is AdmissionCrmPolicyEvaluationStatus.Satisfied or AdmissionCrmPolicyEvaluationStatus.NotApplicable)
			throw new BusinessException("AdmissionCrm:PolicyExceptionNotRequired");
		var entity = new AdmissionCrmPolicyException(GuidGenerator.Create(), CurrentTenant.Id,
			evaluation.AcademicYearId, evaluation.LeadId, evaluation.Id, input.Reason, input.EvidenceReference,
			GetCurrentUserId(), Clock.Now, input.ExpiresAt);
		await _exceptionRepository.InsertAsync(entity, autoSave: true);
		await AddTimelineAsync(lead, AdmissionCrmTimelineEventType.PolicyExceptionRequested,
			"Policy exception requested", input.Reason, entity.Id);
		return MapException(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ApprovePolicyException)]
	public async Task<AdmissionCrmPolicyExceptionDto> DecideExceptionAsync(Guid id, DecideAdmissionCrmPolicyExceptionDto input)
	{
		var entity = await _exceptionRepository.GetAsync(id);
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(entity.AcademicYearId, nameof(DecideExceptionAsync));
		entity.Decide(input.Approve, input.Reason, GetCurrentUserId(), Clock.Now);
		await _exceptionRepository.UpdateAsync(entity, autoSave: true);
		if (input.Approve)
		{
			var evaluation = await GetEvaluationAsync(entity.EvaluationId);
			evaluation.ApplyApprovedException(entity.Id);
			await _evaluationRepository.UpdateAsync(evaluation, autoSave: true);
		}
		var lead = await GetLeadAsync(entity.LeadId, nameof(DecideExceptionAsync));
		await AddTimelineAsync(lead, AdmissionCrmTimelineEventType.PolicyExceptionDecided,
			input.Approve ? "Policy exception approved" : "Policy exception rejected", input.Reason, entity.Id);
		return MapException(entity);
	}

	private static bool IsCurrentOfficialBaseline(AdmissionCrmPolicyPack existing, CreateAdmissionCrmPolicyPackDto baseline)
	{
		var sources = Deserialize<IReadOnlyList<AdmissionCrmPolicySourceDto>>(existing.SourcesJson) ?? [];
		var rules = Deserialize<IReadOnlyList<AdmissionCrmPolicyRuleDto>>(existing.RulesJson) ?? [];
		return sources.Any(x => x.VerifiedAt >= OfficialCatalogVerifiedAt) &&
			baseline.Rules.All(expected => rules.Any(actual => actual.Code.Equals(expected.Code, StringComparison.OrdinalIgnoreCase)));
	}

	private static IReadOnlyList<CreateAdmissionCrmPolicyPackDto> BuildOfficialBaselines()
	{
		const string cbseUrl = "https://www.cbse.gov.in/cbsenew/admission.html";
		const string gujaratRteUrl = "https://rte.orpgujarat.com/Content/Docs/ActRules/age_limit.pdf";
		const string gujaratRteRulesUrl = "https://rte.orpgujarat.com/Home/Rules";
		const string gsebUrl = "https://studentserv.gseb.org/";
		const string apaarUrl = "https://apaar.education.gov.in/";
		var reviewDueAt = new DateTime(2027, 1, 11, 0, 0, 0, DateTimeKind.Utc);
		AdmissionCrmPolicySourceDto Source(string authority, string title, string url, DateTime effectiveAt, string note) => new()
		{
			Authority = authority, Title = title, Url = url, PublishedOrEffectiveAt = effectiveAt,
			VerifiedAt = OfficialCatalogVerifiedAt, ApplicabilityNote = note
		};
		AdmissionCrmPolicyRuleDto Rule(string code, string name, AdmissionCrmPolicyRuleType type,
			AdmissionCrmPolicyRuleSeverity severity, string description, string authority, string sourceUrl,
			string? desiredClass = null, string? evidence = null) => new()
		{
			Code = code, Name = name, Type = type, Severity = severity, Description = description,
			DesiredClass = desiredClass, RequiredEvidenceCode = evidence, ManualVerificationRequired = true,
			SourceAuthority = authority, SourceUrl = sourceUrl
		};

		return
		[
			new CreateAdmissionCrmPolicyPackDto
			{
				Code = "CBSE-ADMISSION-CORE", Name = "CBSE admission governance baseline",
				Jurisdiction = "India and applicable State or Union Territory", Board = "CBSE",
				EffectiveFrom = new DateTime(1995, 1, 31, 0, 0, 0, DateTimeKind.Utc), ReviewDueAt = reviewDueAt,
				Sources = [Source("Central Board of Secondary Education", "Admission of students to a school and transfer or migration", cbseUrl,
					new DateTime(1995, 1, 31, 0, 0, 0, DateTimeKind.Utc),
					"Current official CBSE admission publication; annual circulars and the applicable State or Union Territory rules must also be reviewed.")],
				Rules =
				[
					Rule("CBSE-STATE-AGE", "State-governed age eligibility", AdmissionCrmPolicyRuleType.AgeEligibility,
						AdmissionCrmPolicyRuleSeverity.ApprovalRequired, "Verify the applicable State or Union Territory age rule; this baseline does not impose a national CBSE cutoff.", "CBSE", cbseUrl),
					Rule("CBSE-QUALIFYING-EXAM", "Qualifying examination eligibility", AdmissionCrmPolicyRuleType.QualifyingExamination,
						AdmissionCrmPolicyRuleSeverity.Blocking, "Verify the recognised qualifying examination for the class sought.", "CBSE", cbseUrl, evidence: "QUALIFYING_EXAM"),
					Rule("CBSE-TRANSFER-CERTIFICATE", "Transfer certificate verification", AdmissionCrmPolicyRuleType.DocumentEvidence,
						AdmissionCrmPolicyRuleSeverity.Blocking, "Verify the authenticated transfer certificate where the applicant attended another school.", "CBSE", cbseUrl, evidence: "TRANSFER_CERTIFICATE"),
					Rule("CBSE-LATE-ADMISSION-IX-PLUS", "Class IX and above late-admission approval", AdmissionCrmPolicyRuleType.AdmissionDeadline,
						AdmissionCrmPolicyRuleSeverity.ApprovalRequired, "Review the current deadline and competent-authority approval requirements before late admission in Class IX or above.", "CBSE", cbseUrl),
					Rule("CBSE-CLASS-X-DIRECT", "Class X direct-admission restriction", AdmissionCrmPolicyRuleType.ClassEligibility,
						AdmissionCrmPolicyRuleSeverity.ApprovalRequired, "Verify Class IX regular study, result, transfer circumstances, and any required post-facto approval.", "CBSE", cbseUrl, "Class X"),
					Rule("CBSE-CLASS-XII-DIRECT", "Class XII direct-admission restriction", AdmissionCrmPolicyRuleType.ClassEligibility,
						AdmissionCrmPolicyRuleSeverity.ApprovalRequired, "Verify Class XI regular study, result, transfer circumstances, and any required post-facto approval.", "CBSE", cbseUrl, "Class XII"),
					Rule("CBSE-ADMISSION-REGISTER", "Admission register entry", AdmissionCrmPolicyRuleType.AdmissionRegister,
						AdmissionCrmPolicyRuleSeverity.Blocking, "Verify entry in the prescribed admission register before activation is treated as complete.", "CBSE", cbseUrl),
					Rule("CBSE-PERMANENT-NUMBER", "Successive permanent admission number", AdmissionCrmPolicyRuleType.PermanentNumber,
						AdmissionCrmPolicyRuleSeverity.Blocking, "Allocate a successive admission number and restore the original number for a returning student.", "CBSE", cbseUrl)
				]
			},
			new CreateAdmissionCrmPolicyPackDto
			{
				Code = "GUJARAT-RTE-2025", Name = "Gujarat RTE governed admission baseline",
				Jurisdiction = "Gujarat", Board = "Gujarat RTE",
				EffectiveFrom = new DateTime(2025, 5, 28, 0, 0, 0, DateTimeKind.Utc), ReviewDueAt = reviewDueAt,
				Sources =
				[
					Source("Government of Gujarat Education Department", "Right of Children to Free and Compulsory Education Amendment Rules, 2025", gujaratRteUrl,
						new DateTime(2025, 5, 28, 0, 0, 0, DateTimeKind.Utc), "Official age-limit amendment; cycle adoption must set the applicable June 1 cutoff date."),
					Source("Gujarat Department of Primary Education", "RTE rules, regulations and advertisements", gujaratRteRulesUrl,
						OfficialCatalogVerifiedAt, "Official publication index; review current allotment, evidence, and deadline notices for each cycle.")
				],
				Rules =
				[
					new AdmissionCrmPolicyRuleDto
					{
						Code = "GJ-RTE-STD1-AGE", Name = "Standard I age on June 1", Type = AdmissionCrmPolicyRuleType.AgeEligibility,
						Severity = AdmissionCrmPolicyRuleSeverity.ApprovalRequired, Description = "Set the cycle-specific June 1 cutoff and verify that the child is at least six years old.",
						DesiredClass = "Standard I", MinimumAgeYears = 6, ManualVerificationRequired = true,
						SourceAuthority = "Government of Gujarat Education Department", SourceUrl = gujaratRteUrl
					},
					Rule("GJ-RTE-CWSN-RELAXATION", "CwSN age-relaxation review", AdmissionCrmPolicyRuleType.AgeEligibility,
						AdmissionCrmPolicyRuleSeverity.ApprovalRequired, "Verify disability evidence and the applicable relaxation before overriding the standard age evaluation.", "Government of Gujarat Education Department", gujaratRteUrl, "Standard I", "CWSN_EVIDENCE"),
					Rule("GJ-RTE-ALLOTMENT", "Official RTE allotment verification", AdmissionCrmPolicyRuleType.Allotment,
						AdmissionCrmPolicyRuleSeverity.Blocking, "Verify the current-cycle authority allotment reference and school mapping.", "Gujarat Department of Primary Education", gujaratRteRulesUrl, evidence: "RTE_ALLOTMENT"),
					Rule("GJ-RTE-CYCLE-NOTICES", "Current-cycle RTE notices", AdmissionCrmPolicyRuleType.AdmissionDeadline,
						AdmissionCrmPolicyRuleSeverity.ApprovalRequired, "Review current official deadlines, documents, and exceptions before publishing the cycle adoption.", "Gujarat Department of Primary Education", gujaratRteRulesUrl)
				]
			},
			new CreateAdmissionCrmPolicyPackDto
			{
				Code = "GUJARAT-BOARD-READINESS", Name = "Gujarat Board admission readiness baseline",
				Jurisdiction = "Gujarat", Board = "GSHSEB",
				EffectiveFrom = OfficialCatalogVerifiedAt, ReviewDueAt = reviewDueAt,
				Sources = [Source("Gujarat Secondary and Higher Secondary Education Board", "GSHSEB student services and official instructions", gsebUrl,
					OfficialCatalogVerifiedAt, "Governed review pointer; schools must configure current class-specific GSHSEB instructions rather than infer requirements from this baseline.")],
				Rules =
				[
					Rule("GSHSEB-PRIOR-RECORD", "Previous academic record verification", AdmissionCrmPolicyRuleType.QualifyingExamination,
						AdmissionCrmPolicyRuleSeverity.Warning, "Verify previous marksheet or result evidence against the current class and route requirements.", "GSHSEB", gsebUrl, evidence: "PREVIOUS_MARKSHEET"),
					Rule("GSHSEB-MIGRATION-REVIEW", "Migration certificate applicability", AdmissionCrmPolicyRuleType.DocumentEvidence,
						AdmissionCrmPolicyRuleSeverity.Warning, "Review whether the current route and board change require migration evidence.", "GSHSEB", gsebUrl, evidence: "MIGRATION_CERTIFICATE"),
					Rule("GSHSEB-CURRENT-INSTRUCTIONS", "Current GSHSEB instruction review", AdmissionCrmPolicyRuleType.AuthorityApproval,
						AdmissionCrmPolicyRuleSeverity.ApprovalRequired, "Record review of the applicable current GSHSEB instructions before final activation.", "GSHSEB", gsebUrl)
				]
			},
			new CreateAdmissionCrmPolicyPackDto
			{
				Code = "APAAR-IDENTITY-READINESS", Name = "APAAR, PEN, and UDISE+ identity readiness baseline",
				Jurisdiction = "India", Board = "Ministry of Education",
				EffectiveFrom = OfficialCatalogVerifiedAt, ReviewDueAt = reviewDueAt,
				Sources = [Source("Ministry of Education and Digital India Corporation", "Official APAAR service and FAQs", apaarUrl,
					OfficialCatalogVerifiedAt, "Identity-readiness guidance; this pack does not make APAAR a universal admission prerequisite.")],
				Rules =
				[
					Rule("APAAR-PEN-REQUIRED", "PEN available for APAAR generation", AdmissionCrmPolicyRuleType.GovernmentIdentifier,
						AdmissionCrmPolicyRuleSeverity.Warning, "Record and verify the student's PEN before an APAAR generation handoff.", "Ministry of Education", apaarUrl, evidence: "PEN"),
					Rule("APAAR-DEMOGRAPHIC-MATCH", "UDISE+ and Aadhaar demographic match", AdmissionCrmPolicyRuleType.GovernmentIdentifier,
						AdmissionCrmPolicyRuleSeverity.Warning, "Verify the UDISE+ student name against the Aadhaar name and resolve discrepancies before generation.", "Ministry of Education", apaarUrl, evidence: "IDENTITY_MATCH"),
					Rule("APAAR-MINOR-CONSENT", "Parental consent for a minor", AdmissionCrmPolicyRuleType.Consent,
						AdmissionCrmPolicyRuleSeverity.Blocking, "Preserve voluntary parental consent before requesting APAAR generation for a minor.", "Ministry of Education", apaarUrl, evidence: "APAAR_PARENT_CONSENT")
				]
			}
		];
	}

	private static void ValidatePack(CreateAdmissionCrmPolicyPackDto input)
	{
		if (input.Sources.Count == 0 || input.Rules.Count == 0)
			throw new BusinessException("AdmissionCrm:PolicySourcesAndRulesRequired");
		if (input.Sources.Any(x => !Uri.TryCreate(x.Url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
			throw new BusinessException("AdmissionCrm:OfficialHttpsPolicySourceRequired");
		if (input.Sources.Any(x => x.VerifiedAt > DateTime.UtcNow.AddMinutes(5)))
			throw new BusinessException("AdmissionCrm:InvalidPolicySourceVerificationDate");
		var codes = input.Rules.Select(x => x.Code.Trim().ToUpperInvariant()).ToList();
		if (codes.Count != codes.Distinct().Count()) throw new BusinessException("AdmissionCrm:DuplicatePolicyRuleCode");
		if (input.Rules.Any(x => string.IsNullOrWhiteSpace(x.SourceUrl) || input.Sources.All(s => !s.Url.Equals(x.SourceUrl, StringComparison.OrdinalIgnoreCase))))
			throw new BusinessException("AdmissionCrm:PolicyRuleSourceNotRegistered");
	}

	private static (AdmissionCrmPolicyEvaluationStatus Status, string Explanation) Evaluate(
		AdmissionInquiryLead lead, AdmissionCrmPolicyRuleDto rule, EvaluateAdmissionCrmPolicyRuleDto input)
	{
		if (!string.IsNullOrWhiteSpace(rule.DesiredClass) && !string.Equals(rule.DesiredClass, lead.DesiredClassName, StringComparison.OrdinalIgnoreCase))
			return (AdmissionCrmPolicyEvaluationStatus.NotApplicable, "Rule does not apply to the applicant's desired class.");
		if (rule.Type == AdmissionCrmPolicyRuleType.AgeEligibility && rule.AgeCutoffDate.HasValue &&
			(rule.MinimumAgeYears.HasValue || rule.MaximumAgeYears.HasValue))
		{
			if (!lead.DateOfBirth.HasValue) return (AdmissionCrmPolicyEvaluationStatus.EvidencePending, "Verified date of birth is required.");
			var dob = lead.DateOfBirth.Value.Date;
			var cutoff = rule.AgeCutoffDate.Value.Date;
			var age = cutoff.Year - dob.Year - (dob > cutoff.AddYears(-(cutoff.Year - dob.Year)) ? 1 : 0);
			var satisfied = (!rule.MinimumAgeYears.HasValue || age >= rule.MinimumAgeYears) &&
				(!rule.MaximumAgeYears.HasValue || age <= rule.MaximumAgeYears);
			return (satisfied ? AdmissionCrmPolicyEvaluationStatus.Satisfied : AdmissionCrmPolicyEvaluationStatus.NotSatisfied,
				$"Applicant age is {age} years on {cutoff:yyyy-MM-dd}.");
		}
		if (input.ManualStatus.HasValue)
			return (input.ManualStatus.Value, input.Explanation ?? "Staff recorded a governed manual evaluation.");
		return (rule.ManualVerificationRequired ? AdmissionCrmPolicyEvaluationStatus.ManualReviewRequired : AdmissionCrmPolicyEvaluationStatus.EvidencePending,
			input.Explanation ?? "Evidence or manual verification is required.");
	}

	private static AdmissionCrmPolicyRuleDto ApplyOverride(AdmissionCrmPolicyRuleDto source, IReadOnlyList<AdmissionCrmPolicyOverrideDto> overrides)
	{
		var value = JsonSerializer.Deserialize<AdmissionCrmPolicyRuleDto>(JsonSerializer.Serialize(source, JsonOptions), JsonOptions)!;
		var item = overrides.FirstOrDefault(x => x.RuleCode.Equals(source.Code, StringComparison.OrdinalIgnoreCase));
		if (item == null) return value;
		value.Severity = item.Severity ?? value.Severity;
		value.AgeCutoffDate = item.AgeCutoffDate ?? value.AgeCutoffDate;
		value.MinimumAgeYears = item.MinimumAgeYears ?? value.MinimumAgeYears;
		value.MaximumAgeYears = item.MaximumAgeYears ?? value.MaximumAgeYears;
		value.Deadline = item.Deadline ?? value.Deadline;
		return value;
	}

	private async Task<AdmissionCrmPolicyPack> GetPackAsync(Guid id)
	{
		var pack = await _packRepository.GetAsync(id);
		if (pack.TenantId != CurrentTenant.Id) throw new AbpAuthorizationException("Policy pack belongs to another tenant.");
		return pack;
	}

	private async Task<AdmissionCrmPolicyAdoption> GetAdoptionAsync(Guid id, Guid academicYearId)
	{
		var adoption = await _adoptionRepository.GetAsync(id);
		if (adoption.TenantId != CurrentTenant.Id || adoption.AcademicYearId != academicYearId || !adoption.IsActive)
			throw new AbpAuthorizationException("Policy adoption is not available for this admission cycle.");
		return adoption;
	}

	private async Task<AdmissionInquiryLead> GetLeadAsync(Guid id, string operation)
	{
		var lead = await _leadRepository.GetAsync(id);
		await _studentScopeAccessService.EnsureCanAccessAdmissionLeadAsync(lead, operation);
		return lead;
	}

	private async Task<AdmissionCrmPolicyEvaluation> GetEvaluationAsync(Guid id)
	{
		var evaluation = await _evaluationRepository.GetAsync(id);
		var lead = await _leadRepository.GetAsync(evaluation.LeadId);
		await _studentScopeAccessService.EnsureCanAccessAdmissionLeadAsync(lead, nameof(GetEvaluationAsync));
		return evaluation;
	}

	private async Task AddTimelineAsync(AdmissionInquiryLead lead, AdmissionCrmTimelineEventType type,
		string title, string details, Guid relatedId) => await _timelineRepository.InsertAsync(
		new AdmissionCrmTimelineEvent(GuidGenerator.Create(), CurrentTenant.Id, lead.Id, lead.AcademicYearId,
			type, title, details, relatedId, CurrentUser.Id, Clock.Now), autoSave: true);

	private AdmissionCrmPolicyPackDto MapPack(AdmissionCrmPolicyPack x) => new()
	{
		Id = x.Id, Code = x.Code, Name = x.Name, Jurisdiction = x.Jurisdiction, Board = x.Board,
		Version = x.Version, Status = x.Status, Sources = Deserialize<IReadOnlyList<AdmissionCrmPolicySourceDto>>(x.SourcesJson) ?? [],
		Rules = Deserialize<IReadOnlyList<AdmissionCrmPolicyRuleDto>>(x.RulesJson) ?? [], EffectiveFrom = x.EffectiveFrom,
		EffectiveUntil = x.EffectiveUntil, ReviewDueAt = x.ReviewDueAt, SourceReviewOverdue = x.ReviewDueAt < Clock.Now,
		SubmittedByUserId = x.SubmittedByUserId, SubmittedAt = x.SubmittedAt, ApprovedByUserId = x.ApprovedByUserId,
		ApprovedAt = x.ApprovedAt, PublishedAt = x.PublishedAt, SupersededByPolicyPackId = x.SupersededByPolicyPackId
	};

	private static AdmissionCrmPolicyAdoptionDto MapAdoption(AdmissionCrmPolicyAdoption x)
	{
		var preview = Deserialize<Dictionary<string, IReadOnlyList<string>>>(x.ImpactPreviewJson);
		return new AdmissionCrmPolicyAdoptionDto
		{
			Id = x.Id, AcademicYearId = x.AcademicYearId, PolicyPackId = x.PolicyPackId,
			PolicyPackVersion = x.PolicyPackVersion, Applicability = Deserialize<AdmissionCrmPolicyApplicabilityDto>(x.ApplicabilityJson) ?? new(),
			Overrides = Deserialize<IReadOnlyList<AdmissionCrmPolicyOverrideDto>>(x.OverridesJson) ?? [],
			ImpactWarnings = preview?.GetValueOrDefault("warnings") ?? [], OverrideReason = x.OverrideReason,
			EffectiveFrom = x.EffectiveFrom, EffectiveUntil = x.EffectiveUntil, AdoptedByUserId = x.AdoptedByUserId,
			AdoptedAt = x.AdoptedAt, IsActive = x.IsActive
		};
	}

	private static AdmissionCrmPolicyEvaluationDto MapEvaluation(AdmissionCrmPolicyEvaluation x) => new()
	{
		Id = x.Id, LeadId = x.LeadId, PolicyAdoptionId = x.PolicyAdoptionId, RuleCode = x.RuleCode,
		RuleType = x.RuleType, Severity = x.Severity, Status = x.Status,
		InputSnapshot = Deserialize<Dictionary<string, object?>>(x.InputSnapshotJson) ?? new(),
		EvidenceReferences = Deserialize<IReadOnlyList<string>>(x.EvidenceJson) ?? [], Explanation = x.Explanation,
		EvaluatedAt = x.EvaluatedAt, ExceptionId = x.ExceptionId
	};

	private static AdmissionCrmPolicyExceptionDto MapException(AdmissionCrmPolicyException x) => new()
	{
		Id = x.Id, LeadId = x.LeadId, EvaluationId = x.EvaluationId, Status = x.Status, Reason = x.Reason,
		EvidenceReference = x.EvidenceReference, RequestedByUserId = x.RequestedByUserId, RequestedAt = x.RequestedAt,
		DecidedByUserId = x.DecidedByUserId, DecidedAt = x.DecidedAt, DecisionReason = x.DecisionReason, ExpiresAt = x.ExpiresAt
	};

	private Guid GetCurrentUserId() => CurrentUser.Id ?? throw new AbpAuthorizationException("An authenticated user is required.");
	private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
	private static T? Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value, JsonOptions);
}

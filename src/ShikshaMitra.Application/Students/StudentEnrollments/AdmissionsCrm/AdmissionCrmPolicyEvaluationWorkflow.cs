using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPolicyEvaluationWorkflow(
	IRepository<AdmissionCrmPolicyPack, Guid> packRepository,
	IRepository<AdmissionCrmPolicyAdoption, Guid> adoptionRepository,
	IRepository<AdmissionCrmPolicyEvaluation, Guid> evaluationRepository,
	IRepository<AdmissionCrmPolicyException, Guid> exceptionRepository,
	AdmissionCrmLeadAccessReader leadAccessReader,
	AdmissionCrmPolicyTimelineWriter timelineWriter,
	StudentScopeAccessService studentScopeAccessService) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmPolicyEvaluationDto> EvaluateAsync(
		Guid leadId,
		EvaluateAdmissionCrmPolicyRuleDto input,
		string operation)
	{
		var lead = await leadAccessReader.GetAsync(leadId, operation);
		var adoption = await GetAdoptionAsync(input.PolicyAdoptionId, lead.AcademicYearId);
		var pack = await GetPackAsync(adoption.PolicyPackId);
		var rules = AdmissionCrmPolicyMapper.Deserialize<IReadOnlyList<AdmissionCrmPolicyRuleDto>>(pack.RulesJson) ?? [];
		var rule = rules.SingleOrDefault(item => item.Code.Equals(input.RuleCode, StringComparison.OrdinalIgnoreCase)) ??
			throw new BusinessException("AdmissionCrm:PolicyRuleNotFound");
		var effectiveRule = AdmissionCrmPolicyRuleEvaluator.ApplyOverride(
			rule,
			AdmissionCrmPolicyMapper.Deserialize<IReadOnlyList<AdmissionCrmPolicyOverrideDto>>(adoption.OverridesJson) ?? []);
		var evaluated = AdmissionCrmPolicyRuleEvaluator.Evaluate(lead, effectiveRule, input);
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
		var entity = new AdmissionCrmPolicyEvaluation(
			GuidGenerator.Create(), CurrentTenant.Id, lead.AcademicYearId, lead.Id, adoption.Id,
			effectiveRule.Code, effectiveRule.Type, effectiveRule.Severity, evaluated.Status,
			AdmissionCrmPolicyMapper.Serialize(snapshot),
			AdmissionCrmPolicyMapper.Serialize(input.EvidenceReferences),
			evaluated.Explanation, GetCurrentUserId(), Clock.Now);
		await evaluationRepository.InsertAsync(entity, autoSave: true);
		await timelineWriter.AddAsync(
			lead, AdmissionCrmTimelineEventType.PolicyEvaluated,
			$"Policy evaluated: {effectiveRule.Name}", evaluated.Explanation, entity.Id);
		return AdmissionCrmPolicyMapper.Map(entity);
	}

	public async Task<AdmissionCrmPolicyReadinessDto> GetReadinessAsync(Guid leadId, string operation)
	{
		await leadAccessReader.GetAsync(leadId, operation);
		var evaluationQuery = await evaluationRepository.GetQueryableAsync();
		var evaluations = await AsyncExecuter.ToListAsync(evaluationQuery.Where(entity =>
			entity.TenantId == CurrentTenant.Id && entity.LeadId == leadId));
		var latest = evaluations.GroupBy(entity => new { entity.PolicyAdoptionId, entity.RuleCode })
			.Select(group => group.OrderByDescending(entity => entity.EvaluatedAt).First())
			.OrderBy(entity => entity.RuleCode).ToList();
		var exceptionQuery = await exceptionRepository.GetQueryableAsync();
		var exceptions = await AsyncExecuter.ToListAsync(exceptionQuery.Where(entity =>
			entity.TenantId == CurrentTenant.Id && entity.LeadId == leadId));
		var blocking = latest.Count(entity =>
			entity.Severity is AdmissionCrmPolicyRuleSeverity.Blocking or AdmissionCrmPolicyRuleSeverity.ApprovalRequired &&
			entity.Status is AdmissionCrmPolicyEvaluationStatus.NotSatisfied or AdmissionCrmPolicyEvaluationStatus.ManualReviewRequired);
		var evidencePending = latest.Count(entity => entity.Status == AdmissionCrmPolicyEvaluationStatus.EvidencePending);
		return new AdmissionCrmPolicyReadinessDto
		{
			LeadId = leadId,
			Evaluations = latest.Select(AdmissionCrmPolicyMapper.Map).ToList(),
			Exceptions = exceptions.OrderByDescending(entity => entity.RequestedAt).Select(AdmissionCrmPolicyMapper.Map).ToList(),
			BlockingFailureCount = blocking,
			PendingEvidenceCount = evidencePending,
			IsPolicyReady = latest.Count > 0 && blocking == 0 && evidencePending == 0
		};
	}

	public async Task<AdmissionCrmPolicyExceptionDto> RequestExceptionAsync(
		Guid evaluationId,
		RequestAdmissionCrmPolicyExceptionDto input,
		string operation)
	{
		var evaluation = await GetEvaluationAsync(evaluationId);
		var lead = await leadAccessReader.GetAsync(evaluation.LeadId, operation);
		if (evaluation.Status is AdmissionCrmPolicyEvaluationStatus.Satisfied or AdmissionCrmPolicyEvaluationStatus.NotApplicable)
			throw new BusinessException("AdmissionCrm:PolicyExceptionNotRequired");
		var entity = new AdmissionCrmPolicyException(
			GuidGenerator.Create(), CurrentTenant.Id, evaluation.AcademicYearId, evaluation.LeadId,
			evaluation.Id, input.Reason, input.EvidenceReference, GetCurrentUserId(), Clock.Now, input.ExpiresAt);
		await exceptionRepository.InsertAsync(entity, autoSave: true);
		await timelineWriter.AddAsync(
			lead, AdmissionCrmTimelineEventType.PolicyExceptionRequested,
			"Policy exception requested", input.Reason, entity.Id);
		return AdmissionCrmPolicyMapper.Map(entity);
	}

	public async Task<AdmissionCrmPolicyExceptionDto> DecideExceptionAsync(
		Guid id,
		DecideAdmissionCrmPolicyExceptionDto input,
		string operation)
	{
		var entity = await exceptionRepository.GetAsync(id);
		await studentScopeAccessService.EnsureCanAccessAcademicYearAsync(entity.AcademicYearId, operation);
		entity.Decide(input.Approve, input.Reason, GetCurrentUserId(), Clock.Now);
		await exceptionRepository.UpdateAsync(entity, autoSave: true);
		if (input.Approve)
		{
			var evaluation = await GetEvaluationAsync(entity.EvaluationId);
			evaluation.ApplyApprovedException(entity.Id);
			await evaluationRepository.UpdateAsync(evaluation, autoSave: true);
		}
		var lead = await leadAccessReader.GetAsync(entity.LeadId, operation);
		await timelineWriter.AddAsync(
			lead, AdmissionCrmTimelineEventType.PolicyExceptionDecided,
			input.Approve ? "Policy exception approved" : "Policy exception rejected",
			input.Reason, entity.Id);
		return AdmissionCrmPolicyMapper.Map(entity);
	}

	private async Task<AdmissionCrmPolicyPack> GetPackAsync(Guid id)
	{
		var pack = await packRepository.GetAsync(id);
		if (pack.TenantId != CurrentTenant.Id)
			throw new AbpAuthorizationException("Policy pack belongs to another tenant.");
		return pack;
	}

	private async Task<AdmissionCrmPolicyAdoption> GetAdoptionAsync(Guid id, Guid academicYearId)
	{
		var adoption = await adoptionRepository.GetAsync(id);
		if (adoption.TenantId != CurrentTenant.Id || adoption.AcademicYearId != academicYearId || !adoption.IsActive)
			throw new AbpAuthorizationException("Policy adoption is not available for this admission year.");
		return adoption;
	}

	private async Task<AdmissionCrmPolicyEvaluation> GetEvaluationAsync(Guid id)
	{
		var evaluation = await evaluationRepository.GetAsync(id);
		await leadAccessReader.GetAsync(evaluation.LeadId, nameof(GetEvaluationAsync));
		return evaluation;
	}

	private Guid GetCurrentUserId() =>
		CurrentUser.Id ?? throw new AbpAuthorizationException("An authenticated user is required.");
}

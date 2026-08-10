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

public class AdmissionCrmRetentionPrivacyWorkflow(
	IRepository<AdmissionCrmRetentionPolicy, Guid> retentionRepository,
	IRepository<AdmissionCrmPrivacyRequest, Guid> privacyRepository,
	IRepository<AdmissionCrmActivation, Guid> activationRepository,
	IRepository<AdmissionCrmAdmissionQuotation, Guid> quotationRepository,
	IRepository<AdmissionCrmRouteAssignment, Guid> routeRepository,
	IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository,
	AdmissionCrmGovernanceAccess access)
	: ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmRetentionPolicyDto> CreateRetentionPolicyAsync(Guid academicYearId, CreateAdmissionCrmRetentionPolicyDto input)
	{
		await access.EnsureRetentionMutationAccessAsync(academicYearId, nameof(CreateRetentionPolicyAsync));
		var queryable = await retentionRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId && x.Code == input.Code.ToUpper())))
			throw new BusinessException("AdmissionCrm:RetentionPolicyCodeAlreadyExists");
		var entity = new AdmissionCrmRetentionPolicy(GuidGenerator.Create(), CurrentTenant.Id, academicYearId,
			input.Code, input.Name, input.Category, input.Action, input.RetainDays,
			AdmissionCrmGovernanceMapper.Serialize(input.Scope), input.AuthorityNote);
		await retentionRepository.InsertAsync(entity, autoSave: true);
		return AdmissionCrmGovernanceMapper.Retention(entity);
	}

	public async Task<IReadOnlyList<AdmissionCrmRetentionPolicyDto>> GetRetentionPoliciesAsync(Guid academicYearId)
	{
		await access.EnsureAcademicYearAccessAsync(academicYearId, nameof(GetRetentionPoliciesAsync));
		var queryable = await retentionRepository.GetQueryableAsync();
		var items = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId));
		return items.OrderBy(x => x.Category).ThenBy(x => x.Code).Select(AdmissionCrmGovernanceMapper.Retention).ToList();
	}

	public Task<AdmissionCrmRetentionPolicyDto> SubmitRetentionPolicyAsync(Guid id) =>
		MutateRetentionAsync(id, nameof(SubmitRetentionPolicyAsync), entity => entity.Submit(UserId()));

	public Task<AdmissionCrmRetentionPolicyDto> PublishRetentionPolicyAsync(Guid id) =>
		MutateRetentionAsync(id, nameof(PublishRetentionPolicyAsync), entity => entity.Publish(UserId(), Clock.Now));

	public async Task<AdmissionCrmPrivacyPreviewDto> PreviewPrivacyRequestAsync(Guid leadId, AdmissionCrmPrivacyRequestType type)
	{
		var lead = await access.GetLeadAsync(leadId, nameof(PreviewPrivacyRequestAsync));
		var policiesQuery = await retentionRepository.GetQueryableAsync();
		var policies = await AsyncExecuter.ToListAsync(policiesQuery.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == lead.AcademicYearId && x.Status == AdmissionCrmGovernedConfigurationStatus.Published));
		var blockers = new List<string>();
		var activationQuery = await activationRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(activationQuery.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == lead.Id && x.StudentId != null)))
			blockers.Add("An activated Student exists; durable Student and admission-register history must be retained.");
		var quoteQuery = await quotationRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(quoteQuery.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == lead.Id && x.FinanceReference != null)))
			blockers.Add("A Finance reference exists and remains governed by Finance retention.");
		var routeQuery = await routeRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(routeQuery.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == lead.Id && x.RouteType == AdmissionCrmRouteType.RteEws)))
			blockers.Add("A governed RTE or EWS route record exists and authority evidence must be reviewed.");
		var planned = type == AdmissionCrmPrivacyRequestType.OptionalConsentWithdrawal
			? new List<string> { "Stop future optional communication and optional service processing.", "Retain the consent withdrawal event." }
			: new List<string> { "Redact eligible pre-admission contact and free-text fields after approval.", "Retain immutable decision, policy, Finance, and audit references.", "Preserve report reconciliation keys using non-public identifiers." };
		return new AdmissionCrmPrivacyPreviewDto
		{
			LeadId = lead.Id, Type = type, RetentionBlockers = blockers, PlannedActions = planned,
			ApplicablePolicyIds = policies.Select(x => x.Id).ToList(), RequiresManualExecution = true
		};
	}

	public async Task<AdmissionCrmPrivacyRequestDto> CreatePrivacyRequestAsync(Guid leadId, CreateAdmissionCrmPrivacyRequestDto input)
	{
		var lead = await access.GetLeadAsync(leadId, nameof(CreatePrivacyRequestAsync));
		var preview = await PreviewPrivacyRequestAsync(leadId, input.Type);
		var entity = new AdmissionCrmPrivacyRequest(GuidGenerator.Create(), CurrentTenant.Id, lead.AcademicYearId,
			lead.Id, input.Type, input.Reason, AdmissionCrmGovernanceMapper.Serialize(preview), UserId(), Clock.Now);
		await privacyRepository.InsertAsync(entity, autoSave: true);
		await TimelineAsync(lead, AdmissionCrmTimelineEventType.PrivacyRequestCreated, "Privacy request created", input.Reason, entity.Id);
		return AdmissionCrmGovernanceMapper.Privacy(entity);
	}

	public Task<AdmissionCrmPrivacyRequestDto> DecidePrivacyRequestAsync(Guid id, DecideAdmissionCrmPrivacyRequestDto input) =>
		MutatePrivacyAsync(id, nameof(DecidePrivacyRequestAsync), AdmissionCrmTimelineEventType.PrivacyRequestDecided,
			"Privacy request decided", input.Reason, entity => entity.Decide(input.Approve, UserId(), input.Reason, Clock.Now));

	public Task<AdmissionCrmPrivacyRequestDto> CompletePrivacyRequestAsync(Guid id, CompleteAdmissionCrmPrivacyRequestDto input) =>
		MutatePrivacyAsync(id, nameof(CompletePrivacyRequestAsync), AdmissionCrmTimelineEventType.PrivacyRequestCompleted,
			"Privacy request completed", input.ExecutionReference, entity => entity.Complete(input.ExecutionReference, Clock.Now));

	public async Task<IReadOnlyList<AdmissionCrmPrivacyRequestDto>> GetPrivacyRequestsAsync(Guid academicYearId)
	{
		await access.EnsureAcademicYearAccessAsync(academicYearId, nameof(GetPrivacyRequestsAsync));
		var scopedLeadIds = await access.GetScopedLeadIdsAsync(academicYearId);
		var queryable = await privacyRepository.GetQueryableAsync();
		var items = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId && scopedLeadIds.Contains(x.LeadId)));
		return items.OrderByDescending(x => x.RequestedAt).Select(AdmissionCrmGovernanceMapper.Privacy).ToList();
	}

	private async Task<AdmissionCrmRetentionPolicyDto> MutateRetentionAsync(Guid id, string operation, Action<AdmissionCrmRetentionPolicy> mutate)
	{
		var entity = await retentionRepository.GetAsync(id);
		await access.EnsureRetentionMutationAccessAsync(entity.AcademicYearId, operation);
		mutate(entity);
		await retentionRepository.UpdateAsync(entity, autoSave: true);
		return AdmissionCrmGovernanceMapper.Retention(entity);
	}

	private async Task<AdmissionCrmPrivacyRequestDto> MutatePrivacyAsync(Guid id, string operation, AdmissionCrmTimelineEventType type,
		string title, string detail, Action<AdmissionCrmPrivacyRequest> mutate)
	{
		var entity = await privacyRepository.GetAsync(id);
		await access.GetLeadAsync(entity.LeadId, "GetPrivacyAsync");
		mutate(entity);
		await privacyRepository.UpdateAsync(entity, autoSave: true);
		var lead = await access.GetLeadAsync(entity.LeadId, operation);
		await TimelineAsync(lead, type, title, detail, entity.Id);
		return AdmissionCrmGovernanceMapper.Privacy(entity);
	}

	private Task TimelineAsync(AdmissionInquiryLead lead, AdmissionCrmTimelineEventType type, string title, string detail, Guid relatedId) =>
		timelineRepository.InsertAsync(new AdmissionCrmTimelineEvent(GuidGenerator.Create(), CurrentTenant.Id, lead.Id,
			lead.AcademicYearId, type, title, detail, relatedId, CurrentUser.Id, Clock.Now), autoSave: true);

	private Guid UserId() => CurrentUser.Id ?? throw new AbpAuthorizationException("Authenticated user required.");
}

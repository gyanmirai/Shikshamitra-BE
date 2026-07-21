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
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmFinancialReadinessAppService : ApplicationService, IAdmissionCrmFinancialReadinessAppService
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private readonly IRepository<AdmissionInquiryLead, Guid> _leadRepository;
	private readonly IRepository<AdmissionCrmFeePolicy, Guid> _policyRepository;
	private readonly IRepository<AdmissionCrmAdmissionQuotation, Guid> _quotationRepository;
	private readonly IRepository<AdmissionCrmConcessionRequest, Guid> _concessionRepository;
	private readonly StudentScopeAccessService _studentScopeAccessService;

	public AdmissionCrmFinancialReadinessAppService(
		IRepository<AdmissionInquiryLead, Guid> leadRepository,
		IRepository<AdmissionCrmFeePolicy, Guid> policyRepository,
		IRepository<AdmissionCrmAdmissionQuotation, Guid> quotationRepository,
		IRepository<AdmissionCrmConcessionRequest, Guid> concessionRepository,
		StudentScopeAccessService studentScopeAccessService)
	{
		_leadRepository = leadRepository;
		_policyRepository = policyRepository;
		_quotationRepository = quotationRepository;
		_concessionRepository = concessionRepository;
		_studentScopeAccessService = studentScopeAccessService;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageFeePolicies)]
	public async Task<AdmissionCrmFeePolicyDto> CreateFeePolicyAsync(
		Guid academicYearId, CreateAdmissionCrmFeePolicyDto input)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(CreateFeePolicyAsync));
		ValidateComponents(input.Components);
		var normalizedCode = input.Code.Trim().ToUpperInvariant();
		var queryable = await _policyRepository.GetQueryableAsync();
		var previous = (await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId && x.Code == normalizedCode)))
			.OrderByDescending(x => x.Version)
			.FirstOrDefault();
		if (previous?.Status is AdmissionCrmFeePolicyStatus.Draft or AdmissionCrmFeePolicyStatus.ApprovalPending)
		{
			throw new BusinessException("AdmissionCrm:UnfinishedFeePolicyVersionExists");
		}
		var policy = new AdmissionCrmFeePolicy(
			GuidGenerator.Create(), CurrentTenant.Id, academicYearId, normalizedCode, input.Name,
			(previous?.Version ?? 0) + 1,
			JsonSerializer.Serialize(input.Applicability, JsonOptions),
			JsonSerializer.Serialize(input.Components, JsonOptions), input.Currency,
			input.GateType, input.GateMilestone, input.RequiredAmount, input.RequiredPercentage,
			input.GracePeriodHours, input.EffectiveFrom, input.EffectiveUntil);
		await _policyRepository.InsertAsync(policy, autoSave: true);
		return MapPolicy(policy);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewFinancialStatus)]
	public async Task<PagedResultDto<AdmissionCrmFeePolicyDto>> GetFeePoliciesAsync(
		Guid academicYearId, GetAdmissionCrmFeePolicyListInput input)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(GetFeePoliciesAsync));
		var queryable = await _policyRepository.GetQueryableAsync();
		var entities = await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId));
		var filtered = entities
			.Where(x => !input.Status.HasValue || x.Status == input.Status.Value)
			.Where(x => string.IsNullOrWhiteSpace(input.Code) ||
				x.Code.Equals(input.Code.Trim(), StringComparison.OrdinalIgnoreCase))
			.OrderBy(x => x.Code)
			.ThenByDescending(x => x.Version)
			.ToList();
		return new PagedResultDto<AdmissionCrmFeePolicyDto>(filtered.Count,
			filtered.Skip(input.SkipCount).Take(input.MaxResultCount).Select(MapPolicy).ToList());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageFeePolicies)]
	public async Task<AdmissionCrmFeePolicyDto> SubmitFeePolicyAsync(Guid id)
	{
		var policy = await GetPolicyAsync(id, nameof(SubmitFeePolicyAsync));
		policy.Submit(GetCurrentUserId(), Clock.Now);
		await _policyRepository.UpdateAsync(policy, autoSave: true);
		return MapPolicy(policy);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ApproveFeePolicies)]
	public async Task<AdmissionCrmFeePolicyDto> ApproveFeePolicyAsync(Guid id)
	{
		var policy = await GetPolicyAsync(id, nameof(ApproveFeePolicyAsync));
		policy.Approve(GetCurrentUserId(), Clock.Now);
		await _policyRepository.UpdateAsync(policy, autoSave: true);
		return MapPolicy(policy);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ApproveFeePolicies)]
	public async Task<AdmissionCrmFeePolicyDto> PublishFeePolicyAsync(Guid id)
	{
		var policy = await GetPolicyAsync(id, nameof(PublishFeePolicyAsync));
		policy.Publish(Clock.Now);
		await _policyRepository.UpdateAsync(policy, autoSave: true);
		return MapPolicy(policy);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.IssueQuotations)]
	public async Task<AdmissionCrmAdmissionQuotationDto> IssueQuotationAsync(
		Guid leadId, IssueAdmissionCrmQuotationDto input)
	{
		var lead = await GetLeadAsync(leadId, nameof(IssueQuotationAsync));
		var policy = await GetPolicyAsync(input.FeePolicyId, nameof(IssueQuotationAsync));
		if (policy.AcademicYearId != lead.AcademicYearId || policy.Status != AdmissionCrmFeePolicyStatus.Published)
		{
			throw new BusinessException("AdmissionCrm:ApplicablePublishedFeePolicyRequired");
		}
		var now = Clock.Now.ToUniversalTime();
		if (policy.EffectiveFrom > now || policy.EffectiveUntil.HasValue && policy.EffectiveUntil.Value < now)
		{
			throw new BusinessException("AdmissionCrm:FeePolicyNotEffective");
		}
		EnsurePolicyMatchesLead(policy, lead);

		var quotationQueryable = await _quotationRepository.GetQueryableAsync();
		var existing = (await AsyncExecuter.ToListAsync(quotationQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == leadId)))
			.OrderByDescending(x => x.Version)
			.ToList();
		foreach (var open in existing.Where(x => x.Status is AdmissionCrmQuotationStatus.Draft or AdmissionCrmQuotationStatus.Issued))
		{
			open.Supersede();
			await _quotationRepository.UpdateAsync(open);
		}

		var concessionQueryable = await _concessionRepository.GetQueryableAsync();
		var concessions = await AsyncExecuter.ToListAsync(concessionQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == leadId &&
			x.Status == AdmissionCrmConcessionStatus.FinanceAcknowledged));
		var components = Deserialize<IReadOnlyList<AdmissionCrmFeeComponentDto>>(policy.ComponentsJson) ?? [];
		var grossAmount = components.Sum(x => x.Amount);
		var concessionAmount = Math.Min(grossAmount, concessions.Sum(x => x.ApprovedAmount));
		var version = (existing.FirstOrDefault()?.Version ?? 0) + 1;
		var quotation = new AdmissionCrmAdmissionQuotation(
			GuidGenerator.Create(), CurrentTenant.Id, lead.AcademicYearId, leadId, policy.Id, policy.Version,
			$"AQ-{GuidGenerator.Create():N}", version, policy.Currency, policy.ComponentsJson,
			grossAmount, concessionAmount, policy.GateType, policy.GateMilestone,
			ResolveRequiredAmount(policy, grossAmount - concessionAmount), input.ValidUntil);
		quotation.Issue(Clock.Now);
		await _quotationRepository.InsertAsync(quotation, autoSave: true);
		return MapQuotation(quotation);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewFinancialStatus)]
	public async Task<AdmissionCrmFinancialReadinessDto> GetFinancialReadinessAsync(Guid leadId)
	{
		await GetLeadAsync(leadId, nameof(GetFinancialReadinessAsync));
		var quotationQueryable = await _quotationRepository.GetQueryableAsync();
		var quotations = (await AsyncExecuter.ToListAsync(quotationQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == leadId)))
			.OrderByDescending(x => x.Version)
			.ToList();
		var concessionQueryable = await _concessionRepository.GetQueryableAsync();
		var concessions = (await AsyncExecuter.ToListAsync(concessionQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == leadId)))
			.OrderByDescending(x => x.RequestedAt)
			.ToList();
		var current = quotations.FirstOrDefault(x => x.Status == AdmissionCrmQuotationStatus.Issued);
		var satisfied = current?.IsGateSatisfiedAt(Clock.Now) ?? true;
		return new AdmissionCrmFinancialReadinessDto
		{
			LeadId = leadId,
			CurrentQuotation = current == null ? null : MapQuotation(current),
			QuotationHistory = quotations.Select(MapQuotation).ToList(),
			Concessions = concessions.Select(MapConcession).ToList(),
			CanAcceptOffer = CanPass(current, AdmissionCrmPaymentGateMilestone.OfferAcceptance, satisfied),
			CanApproveAdmission = CanPass(current, AdmissionCrmPaymentGateMilestone.FinalApproval, satisfied),
			CanActivateStudent = CanPass(current, AdmissionCrmPaymentGateMilestone.StudentActivation, satisfied),
			BlockingReason = current != null && !satisfied
				? $"Payment gate {current.GateType} is not satisfied for {current.GateMilestone}."
				: null
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.RecordFinanceStatus)]
	public async Task<AdmissionCrmAdmissionQuotationDto> RecordFinanceStateAsync(
		Guid quotationId, RecordAdmissionCrmFinanceStateDto input)
	{
		var quotation = await GetQuotationAsync(quotationId, nameof(RecordFinanceStateAsync));
		quotation.RecordFinanceState(input.State, input.ConfirmedAmount, input.FinanceReference,
			input.Source, input.ConfirmedAt);
		await _quotationRepository.UpdateAsync(quotation, autoSave: true);
		return MapQuotation(quotation);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.OverridePaymentGates)]
	public async Task<AdmissionCrmAdmissionQuotationDto> OverridePaymentGateAsync(
		Guid quotationId, OverrideAdmissionCrmPaymentGateDto input)
	{
		var quotation = await GetQuotationAsync(quotationId, nameof(OverridePaymentGateAsync));
		quotation.OverrideGate(input.Reason, GetCurrentUserId(), Clock.Now, input.Until);
		await _quotationRepository.UpdateAsync(quotation, autoSave: true);
		return MapQuotation(quotation);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.RequestConcession)]
	public async Task<AdmissionCrmConcessionRequestDto> RequestConcessionAsync(
		Guid leadId, CreateAdmissionCrmConcessionRequestDto input)
	{
		var lead = await GetLeadAsync(leadId, nameof(RequestConcessionAsync));
		if (input.QuotationId.HasValue)
		{
			var quotation = await GetQuotationAsync(input.QuotationId.Value, nameof(RequestConcessionAsync));
			if (quotation.LeadId != leadId)
			{
				throw new BusinessException("AdmissionCrm:ConcessionQuotationMismatch");
			}
		}
		var request = new AdmissionCrmConcessionRequest(
			GuidGenerator.Create(), CurrentTenant.Id, lead.AcademicYearId, leadId, input.QuotationId,
			input.Type, input.RequestedAmount, input.RequestedPercentage, input.Reason,
			input.EvidenceReference, GetCurrentUserId(), Clock.Now);
		await _concessionRepository.InsertAsync(request, autoSave: true);
		return MapConcession(request);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewFinancialStatus)]
	public async Task<IReadOnlyList<AdmissionCrmConcessionRequestDto>> GetConcessionsAsync(Guid leadId)
	{
		await GetLeadAsync(leadId, nameof(GetConcessionsAsync));
		var queryable = await _concessionRepository.GetQueryableAsync();
		var entities = await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == leadId));
		return entities.OrderByDescending(x => x.RequestedAt).Select(MapConcession).ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ApproveConcession)]
	public async Task<AdmissionCrmConcessionRequestDto> DecideConcessionAsync(
		Guid id, DecideAdmissionCrmConcessionDto input)
	{
		var request = await GetConcessionAsync(id, nameof(DecideConcessionAsync));
		if (input.Approve)
		{
			request.Approve(input.ApprovedAmount, input.Reason, GetCurrentUserId(), Clock.Now);
		}
		else
		{
			request.Reject(input.Reason, GetCurrentUserId(), Clock.Now);
		}
		await _concessionRepository.UpdateAsync(request, autoSave: true);
		return MapConcession(request);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.RecordFinanceStatus)]
	public async Task<AdmissionCrmConcessionRequestDto> AcknowledgeConcessionFinanceAsync(
		Guid id, AcknowledgeAdmissionCrmConcessionFinanceDto input)
	{
		var request = await GetConcessionAsync(id, nameof(AcknowledgeConcessionFinanceAsync));
		request.AcknowledgeFinance(input.FinanceReference, Clock.Now);
		await _concessionRepository.UpdateAsync(request, autoSave: true);
		return MapConcession(request);
	}

	private async Task<AdmissionInquiryLead> GetLeadAsync(Guid id, string operation)
	{
		var lead = await _leadRepository.FindAsync(id);
		if (lead == null || lead.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException("AdmissionCrm:LeadNotFound");
		}
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(lead.AcademicYearId, operation);
		return lead;
	}

	private async Task<AdmissionCrmFeePolicy> GetPolicyAsync(Guid id, string operation)
	{
		var policy = await _policyRepository.FindAsync(id);
		if (policy == null || policy.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException("AdmissionCrm:FeePolicyNotFound");
		}
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(policy.AcademicYearId, operation);
		return policy;
	}

	private async Task<AdmissionCrmAdmissionQuotation> GetQuotationAsync(Guid id, string operation)
	{
		var quotation = await _quotationRepository.FindAsync(id);
		if (quotation == null || quotation.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException("AdmissionCrm:QuotationNotFound");
		}
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(quotation.AcademicYearId, operation);
		return quotation;
	}

	private async Task<AdmissionCrmConcessionRequest> GetConcessionAsync(Guid id, string operation)
	{
		var request = await _concessionRepository.FindAsync(id);
		if (request == null || request.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException("AdmissionCrm:ConcessionNotFound");
		}
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(request.AcademicYearId, operation);
		return request;
	}

	private static void ValidateComponents(IReadOnlyList<AdmissionCrmFeeComponentDto> components)
	{
		if (components == null || components.Count == 0 || components.Any(x =>
			string.IsNullOrWhiteSpace(x.Key) || string.IsNullOrWhiteSpace(x.Name) || x.Amount < 0))
		{
			throw new BusinessException("AdmissionCrm:InvalidFeePolicyComponents");
		}
		if (components.Select(x => x.Key.Trim().ToUpperInvariant()).Distinct().Count() != components.Count)
		{
			throw new BusinessException("AdmissionCrm:DuplicateFeePolicyComponentKey");
		}
	}

	private static void EnsurePolicyMatchesLead(AdmissionCrmFeePolicy policy, AdmissionInquiryLead lead)
	{
		var applicability = Deserialize<AdmissionCrmFeePolicyApplicabilityDto>(policy.ApplicabilityJson) ?? new();
		if (!Matches(applicability.Campus, lead.CampusBranchName) ||
			!Matches(applicability.DesiredClass, lead.DesiredClassName))
		{
			throw new BusinessException("AdmissionCrm:FeePolicyNotApplicableToLead");
		}
	}

	private static bool Matches(string? expected, string? actual) =>
		string.IsNullOrWhiteSpace(expected) || string.Equals(expected.Trim(), actual?.Trim(), StringComparison.OrdinalIgnoreCase);

	private static decimal ResolveRequiredAmount(AdmissionCrmFeePolicy policy, decimal netAmount)
	{
		var percentageAmount = policy.RequiredPercentage <= 0
			? 0
			: Math.Round(netAmount * policy.RequiredPercentage / 100m, 2);
		return Math.Min(netAmount, Math.Max(policy.RequiredAmount, percentageAmount));
	}

	private static bool CanPass(
		AdmissionCrmAdmissionQuotation? quotation, AdmissionCrmPaymentGateMilestone milestone, bool satisfied) =>
		quotation == null || quotation.GateMilestone > milestone || satisfied;

	private Guid GetCurrentUserId() => CurrentUser.Id ??
		throw new BusinessException("AdmissionCrm:AuthenticatedUserRequired");

	private AdmissionCrmAdmissionQuotationDto MapQuotation(AdmissionCrmAdmissionQuotation entity) => new()
	{
		Id = entity.Id,
		AcademicYearId = entity.AcademicYearId,
		LeadId = entity.LeadId,
		FeePolicyId = entity.FeePolicyId,
		FeePolicyVersion = entity.FeePolicyVersion,
		QuotationNumber = entity.QuotationNumber,
		Version = entity.Version,
		Status = entity.Status,
		Currency = entity.Currency,
		Components = Deserialize<IReadOnlyList<AdmissionCrmFeeComponentDto>>(entity.ComponentsSnapshotJson) ?? [],
		GrossAmount = entity.GrossAmount,
		ConcessionAmount = entity.ConcessionAmount,
		NetAmount = entity.NetAmount,
		GateType = entity.GateType,
		GateMilestone = entity.GateMilestone,
		RequiredAmount = entity.RequiredAmount,
		FinanceState = entity.FinanceState,
		ConfirmedAmount = entity.ConfirmedAmount,
		FinanceReference = entity.FinanceReference,
		FinanceStateSource = entity.FinanceStateSource,
		FinanceConfirmedAt = entity.FinanceConfirmedAt,
		ValidUntil = entity.ValidUntil,
		IssuedAt = entity.IssuedAt,
		GateOverrideUsed = entity.GateOverrideUsed,
		GateOverrideReason = entity.GateOverrideReason,
		GateOverrideUntil = entity.GateOverrideUntil,
		IsGateSatisfied = entity.IsGateSatisfiedAt(Clock.Now)
	};

	private static AdmissionCrmFeePolicyDto MapPolicy(AdmissionCrmFeePolicy entity) => new()
	{
		Id = entity.Id,
		AcademicYearId = entity.AcademicYearId,
		Code = entity.Code,
		Name = entity.Name,
		Version = entity.Version,
		Status = entity.Status,
		Applicability = Deserialize<AdmissionCrmFeePolicyApplicabilityDto>(entity.ApplicabilityJson) ?? new(),
		Components = Deserialize<IReadOnlyList<AdmissionCrmFeeComponentDto>>(entity.ComponentsJson) ?? [],
		Currency = entity.Currency,
		GateType = entity.GateType,
		GateMilestone = entity.GateMilestone,
		RequiredAmount = entity.RequiredAmount,
		RequiredPercentage = entity.RequiredPercentage,
		GracePeriodHours = entity.GracePeriodHours,
		EffectiveFrom = entity.EffectiveFrom,
		EffectiveUntil = entity.EffectiveUntil,
		SubmittedByUserId = entity.SubmittedByUserId,
		SubmittedAt = entity.SubmittedAt,
		ApprovedByUserId = entity.ApprovedByUserId,
		ApprovedAt = entity.ApprovedAt,
		PublishedAt = entity.PublishedAt,
		ConcurrencyStamp = entity.ConcurrencyStamp
	};

	private static AdmissionCrmConcessionRequestDto MapConcession(AdmissionCrmConcessionRequest entity) => new()
	{
		Id = entity.Id,
		AcademicYearId = entity.AcademicYearId,
		LeadId = entity.LeadId,
		QuotationId = entity.QuotationId,
		Type = entity.Type,
		Status = entity.Status,
		RequestedAmount = entity.RequestedAmount,
		RequestedPercentage = entity.RequestedPercentage,
		ApprovedAmount = entity.ApprovedAmount,
		Reason = entity.Reason,
		EvidenceReference = entity.EvidenceReference,
		RequestedByUserId = entity.RequestedByUserId,
		RequestedAt = entity.RequestedAt,
		ApprovedByUserId = entity.ApprovedByUserId,
		ApprovedAt = entity.ApprovedAt,
		DecisionReason = entity.DecisionReason,
		FinanceReference = entity.FinanceReference,
		FinanceAcknowledgedAt = entity.FinanceAcknowledgedAt
	};

	private static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonOptions);
}

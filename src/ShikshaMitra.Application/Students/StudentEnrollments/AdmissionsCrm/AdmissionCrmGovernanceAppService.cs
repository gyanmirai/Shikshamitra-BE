using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmGovernanceAppService : ApplicationService, IAdmissionCrmGovernanceAppService
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private readonly IRepository<AdmissionCrmRetentionPolicy, Guid> _retentionRepository;
	private readonly IRepository<AdmissionCrmPrivacyRequest, Guid> _privacyRepository;
	private readonly IRepository<AdmissionCrmExportRequest, Guid> _exportRepository;
	private readonly IRepository<AdmissionCrmReportSchedule, Guid> _scheduleRepository;
	private readonly IRepository<AdmissionCrmWorkDelegation, Guid> _delegationRepository;
	private readonly IRepository<AdmissionInquiryLead, Guid> _leadRepository;
	private readonly IRepository<AdmissionCrmSeatOffer, Guid> _offerRepository;
	private readonly IRepository<AdmissionCrmActivation, Guid> _activationRepository;
	private readonly IRepository<AdmissionCrmAdmissionQuotation, Guid> _quotationRepository;
	private readonly IRepository<AdmissionCrmRouteAssignment, Guid> _routeRepository;
	private readonly IRepository<AdmissionCrmApplicantIdentityProfile, Guid> _identityProfileRepository;
	private readonly IRepository<AdmissionCrmPerson, Guid> _personRepository;
	private readonly IRepository<AdmissionCrmDataQualityIssue, Guid> _dataQualityRepository;
	private readonly IRepository<AdmissionCrmTimelineEvent, Guid> _timelineRepository;
	private readonly StudentScopeAccessService _scopeAccess;

	public AdmissionCrmGovernanceAppService(
		IRepository<AdmissionCrmRetentionPolicy, Guid> retentionRepository,
		IRepository<AdmissionCrmPrivacyRequest, Guid> privacyRepository,
		IRepository<AdmissionCrmExportRequest, Guid> exportRepository,
		IRepository<AdmissionCrmReportSchedule, Guid> scheduleRepository,
		IRepository<AdmissionCrmWorkDelegation, Guid> delegationRepository,
		IRepository<AdmissionInquiryLead, Guid> leadRepository,
		IRepository<AdmissionCrmSeatOffer, Guid> offerRepository,
		IRepository<AdmissionCrmActivation, Guid> activationRepository,
		IRepository<AdmissionCrmAdmissionQuotation, Guid> quotationRepository,
		IRepository<AdmissionCrmRouteAssignment, Guid> routeRepository,
		IRepository<AdmissionCrmApplicantIdentityProfile, Guid> identityProfileRepository,
		IRepository<AdmissionCrmPerson, Guid> personRepository,
		IRepository<AdmissionCrmDataQualityIssue, Guid> dataQualityRepository,
		IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository,
		StudentScopeAccessService scopeAccess)
	{
		_retentionRepository = retentionRepository; _privacyRepository = privacyRepository;
		_exportRepository = exportRepository; _scheduleRepository = scheduleRepository;
		_delegationRepository = delegationRepository; _leadRepository = leadRepository;
		_offerRepository = offerRepository; _activationRepository = activationRepository;
		_quotationRepository = quotationRepository; _routeRepository = routeRepository;
		_identityProfileRepository = identityProfileRepository; _personRepository = personRepository;
		_dataQualityRepository = dataQualityRepository; _timelineRepository = timelineRepository;
		_scopeAccess = scopeAccess;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRetention)]
	public async Task<AdmissionCrmRetentionPolicyDto> CreateRetentionPolicyAsync(
		Guid academicYearId, CreateAdmissionCrmRetentionPolicyDto input)
	{
		await _scopeAccess.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(CreateRetentionPolicyAsync));
		var queryable = await _retentionRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId && x.Code == input.Code.ToUpper())))
			throw new BusinessException("AdmissionCrm:RetentionPolicyCodeAlreadyExists");
		var entity = new AdmissionCrmRetentionPolicy(GuidGenerator.Create(), CurrentTenant.Id, academicYearId,
			input.Code, input.Name, input.Category, input.Action, input.RetainDays,
			Serialize(input.Scope), input.AuthorityNote);
		await _retentionRepository.InsertAsync(entity, autoSave: true);
		return MapRetention(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRetention)]
	public async Task<IReadOnlyList<AdmissionCrmRetentionPolicyDto>> GetRetentionPoliciesAsync(Guid academicYearId)
	{
		await _scopeAccess.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(GetRetentionPoliciesAsync));
		var queryable = await _retentionRepository.GetQueryableAsync();
		var items = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId));
		return items.OrderBy(x => x.Category).ThenBy(x => x.Code).Select(MapRetention).ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRetention)]
	public async Task<AdmissionCrmRetentionPolicyDto> SubmitRetentionPolicyAsync(Guid id)
	{
		var entity = await GetRetentionAsync(id); entity.Submit(UserId());
		await _retentionRepository.UpdateAsync(entity, autoSave: true); return MapRetention(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRetention)]
	public async Task<AdmissionCrmRetentionPolicyDto> PublishRetentionPolicyAsync(Guid id)
	{
		var entity = await GetRetentionAsync(id); entity.Publish(UserId(), Clock.Now);
		await _retentionRepository.UpdateAsync(entity, autoSave: true); return MapRetention(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRetention)]
	public async Task<AdmissionCrmPrivacyPreviewDto> PreviewPrivacyRequestAsync(
		Guid leadId, AdmissionCrmPrivacyRequestType type)
	{
		var lead = await GetLeadAsync(leadId, nameof(PreviewPrivacyRequestAsync));
		var policiesQuery = await _retentionRepository.GetQueryableAsync();
		var policies = await AsyncExecuter.ToListAsync(policiesQuery.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == lead.AcademicYearId && x.Status == AdmissionCrmGovernedConfigurationStatus.Published));
		var blockers = new List<string>();
		var activationQuery = await _activationRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(activationQuery.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == lead.Id && x.StudentId != null)))
			blockers.Add("An activated Student exists; durable Student and admission-register history must be retained.");
		var quoteQuery = await _quotationRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(quoteQuery.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == lead.Id && x.FinanceReference != null)))
			blockers.Add("A Finance reference exists and remains governed by Finance retention.");
		var routeQuery = await _routeRepository.GetQueryableAsync();
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

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRetention)]
	public async Task<AdmissionCrmPrivacyRequestDto> CreatePrivacyRequestAsync(
		Guid leadId, CreateAdmissionCrmPrivacyRequestDto input)
	{
		var lead = await GetLeadAsync(leadId, nameof(CreatePrivacyRequestAsync));
		var preview = await PreviewPrivacyRequestAsync(leadId, input.Type);
		var entity = new AdmissionCrmPrivacyRequest(GuidGenerator.Create(), CurrentTenant.Id, lead.AcademicYearId,
			lead.Id, input.Type, input.Reason, Serialize(preview), UserId(), Clock.Now);
		await _privacyRepository.InsertAsync(entity, autoSave: true);
		await TimelineAsync(lead, AdmissionCrmTimelineEventType.PrivacyRequestCreated, "Privacy request created", input.Reason, entity.Id);
		return MapPrivacy(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRetention)]
	public async Task<AdmissionCrmPrivacyRequestDto> DecidePrivacyRequestAsync(Guid id, DecideAdmissionCrmPrivacyRequestDto input)
	{
		var entity = await GetPrivacyAsync(id); entity.Decide(input.Approve, UserId(), input.Reason, Clock.Now);
		await _privacyRepository.UpdateAsync(entity, autoSave: true);
		var lead = await GetLeadAsync(entity.LeadId, nameof(DecidePrivacyRequestAsync));
		await TimelineAsync(lead, AdmissionCrmTimelineEventType.PrivacyRequestDecided, "Privacy request decided", input.Reason, entity.Id);
		return MapPrivacy(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRetention)]
	public async Task<AdmissionCrmPrivacyRequestDto> CompletePrivacyRequestAsync(Guid id, CompleteAdmissionCrmPrivacyRequestDto input)
	{
		var entity = await GetPrivacyAsync(id); entity.Complete(input.ExecutionReference, Clock.Now);
		await _privacyRepository.UpdateAsync(entity, autoSave: true);
		var lead = await GetLeadAsync(entity.LeadId, nameof(CompletePrivacyRequestAsync));
		await TimelineAsync(lead, AdmissionCrmTimelineEventType.PrivacyRequestCompleted, "Privacy request completed", input.ExecutionReference, entity.Id);
		return MapPrivacy(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRetention)]
	public async Task<IReadOnlyList<AdmissionCrmPrivacyRequestDto>> GetPrivacyRequestsAsync(Guid academicYearId)
	{
		await _scopeAccess.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(GetPrivacyRequestsAsync));
		var queryable = await _privacyRepository.GetQueryableAsync();
		var items = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId));
		return items.OrderByDescending(x => x.RequestedAt).Select(MapPrivacy).ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.SensitiveExport)]
	public async Task<AdmissionCrmExportRequestDto> RequestExportAsync(Guid academicYearId, RequestAdmissionCrmExportDto input)
	{
		await _scopeAccess.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(RequestExportAsync));
		if (input.ExpiresInHours is < 1 or > 168) throw new BusinessException("AdmissionCrm:InvalidExportExpiry");
		var now = Clock.Now; var content = await BuildExportContentAsync(academicYearId, input, now);
		var id = GuidGenerator.Create();
		var entity = new AdmissionCrmExportRequest(id, CurrentTenant.Id, academicYearId, input.ReportType,
			Serialize(input.Filters), $"admissions-{input.ReportType.ToString().ToLowerInvariant()}-{now:yyyyMMddHHmm}-{id:N}.csv",
			content, UserId(), now, now.AddHours(input.ExpiresInHours));
		await _exportRepository.InsertAsync(entity, autoSave: true);
		return MapExport(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.SensitiveExport)]
	public async Task<IReadOnlyList<AdmissionCrmExportRequestDto>> GetExportsAsync(Guid academicYearId)
	{
		await _scopeAccess.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(GetExportsAsync));
		var queryable = await _exportRepository.GetQueryableAsync();
		var items = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId));
		var expired = items.Where(x => x.ExpireContent(Clock.Now)).ToList();
		if (expired.Count > 0) await _exportRepository.UpdateManyAsync(expired, autoSave: true);
		return items.OrderByDescending(x => x.RequestedAt).Take(200).Select(MapExport).ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.SensitiveExport)]
	public async Task<AdmissionCrmExportDownloadDto> DownloadExportAsync(Guid id)
	{
		var entity = await _exportRepository.GetAsync(id);
		await _scopeAccess.EnsureCanAccessAcademicYearAsync(entity.AcademicYearId, nameof(DownloadExportAsync));
		entity.RegisterDownload(UserId(), Clock.Now); await _exportRepository.UpdateAsync(entity, autoSave: true);
		return new AdmissionCrmExportDownloadDto
		{
			FileName = entity.FileName, ContentType = entity.ContentType,
			Base64Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(entity.Content)), ExpiresAt = entity.ExpiresAt
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageScheduledReports)]
	public async Task<AdmissionCrmReportScheduleDto> CreateReportScheduleAsync(Guid academicYearId, CreateAdmissionCrmReportScheduleDto input)
	{
		await _scopeAccess.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(CreateReportScheduleAsync));
		if (!input.Destination.StartsWith("manual:", StringComparison.OrdinalIgnoreCase))
			throw new BusinessException("AdmissionCrm:ApprovedReportDestinationRequired");
		var entity = new AdmissionCrmReportSchedule(GuidGenerator.Create(), CurrentTenant.Id, academicYearId,
			input.Name, input.ReportType, input.Frequency, input.Destination, Serialize(input.Filters), input.NextRunAt, UserId());
		await _scheduleRepository.InsertAsync(entity, autoSave: true); return MapSchedule(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageScheduledReports)]
	public async Task<IReadOnlyList<AdmissionCrmReportScheduleDto>> GetReportSchedulesAsync(Guid academicYearId)
	{
		await _scopeAccess.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(GetReportSchedulesAsync));
		var queryable = await _scheduleRepository.GetQueryableAsync();
		var items = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId));
		return items.OrderBy(x => x.NextRunAt).Select(MapSchedule).ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageScheduledReports)]
	public async Task<AdmissionCrmReportScheduleDto> SetReportScheduleActiveAsync(Guid id, bool active)
	{
		var entity = await _scheduleRepository.GetAsync(id);
		await _scopeAccess.EnsureCanAccessAcademicYearAsync(entity.AcademicYearId, nameof(SetReportScheduleActiveAsync));
		entity.SetActive(active); await _scheduleRepository.UpdateAsync(entity, autoSave: true); return MapSchedule(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageScheduledReports)]
	public Task<AdmissionCrmReportScheduleRunResultDto> RunDueReportSchedulesAsync(Guid academicYearId) =>
		RunDueReportSchedulesCoreAsync(academicYearId);

	[AllowAnonymous]
	public Task<AdmissionCrmReportScheduleRunResultDto> RunDueReportSchedulesFromAutomationAsync(Guid academicYearId) =>
		RunDueReportSchedulesCoreAsync(academicYearId);

	private async Task<AdmissionCrmReportScheduleRunResultDto> RunDueReportSchedulesCoreAsync(Guid academicYearId)
	{
		await _scopeAccess.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(RunDueReportSchedulesCoreAsync));
		var now = Clock.Now;
		var queryable = await _scheduleRepository.GetQueryableAsync();
		var due = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId && x.IsActive && x.NextRunAt <= now));
		var exportIds = new List<Guid>();
		var failedIds = new List<Guid>();
		var failureDetails = new Dictionary<Guid, string>();
		foreach (var schedule in due)
		{
			try
			{
				var request = new RequestAdmissionCrmExportDto
				{
					ReportType = schedule.ReportType,
					Filters = Deserialize<Dictionary<string, string?>>(schedule.FiltersJson) ?? new Dictionary<string, string?>(),
					ExpiresInHours = 24
				};
				var content = await BuildExportContentAsync(academicYearId, request, now);
				var exportId = GuidGenerator.Create();
				await _exportRepository.InsertAsync(new AdmissionCrmExportRequest(
					exportId, CurrentTenant.Id, academicYearId, request.ReportType, schedule.FiltersJson,
					$"admissions-{request.ReportType.ToString().ToLowerInvariant()}-{now:yyyyMMddHHmm}-{exportId:N}.csv",
					content, schedule.CreatedByUserId, now, now.AddHours(24)), autoSave: true);
				schedule.MarkRun(now);
				await _scheduleRepository.UpdateAsync(schedule, autoSave: true);
				exportIds.Add(exportId);
			}
			catch (Exception exception)
			{
				failedIds.Add(schedule.Id);
				failureDetails[schedule.Id] = exception is BusinessException businessException
					? businessException.Code ?? businessException.Message
					: $"{exception.GetType().Name}: {exception.Message}";
				Logger.LogError(exception, "Failed to generate scheduled admissions report {ScheduleId}.", schedule.Id);
			}
		}
		return new AdmissionCrmReportScheduleRunResultDto
		{
			AcademicYearId = academicYearId, EvaluatedCount = due.Count, GeneratedCount = exportIds.Count,
			FailedCount = failedIds.Count, ExportIds = exportIds, FailedScheduleIds = failedIds,
			FailureDetails = failureDetails
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageDelegation)]
	public async Task<AdmissionCrmWorkDelegationDto> CreateDelegationAsync(Guid academicYearId, CreateAdmissionCrmWorkDelegationDto input)
	{
		await _scopeAccess.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(CreateDelegationAsync));
		if (input.QueueTypes.Count == 0 || input.QueueTypes.Any(x => !Enum.IsDefined(x)))
			throw new BusinessException("AdmissionCrm:DelegationQueuesRequired");
		var entity = new AdmissionCrmWorkDelegation(GuidGenerator.Create(), CurrentTenant.Id, academicYearId,
			input.DelegatorUserId, input.DelegateUserId, Serialize(input.QueueTypes.Distinct().ToList()),
			input.StartsAt, input.EndsAt, input.Reason, Clock.Now);
		await _delegationRepository.InsertAsync(entity, autoSave: true); return MapDelegation(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageDelegation)]
	public async Task<IReadOnlyList<AdmissionCrmWorkDelegationDto>> GetDelegationsAsync(Guid academicYearId)
	{
		await _scopeAccess.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(GetDelegationsAsync));
		var queryable = await _delegationRepository.GetQueryableAsync();
		var items = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId));
		foreach (var item in items) item.Refresh(Clock.Now);
		await _delegationRepository.UpdateManyAsync(items, autoSave: true);
		return items.OrderByDescending(x => x.StartsAt).Select(MapDelegation).ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageDelegation)]
	public async Task<AdmissionCrmWorkDelegationDto> CancelDelegationAsync(Guid id)
	{
		var entity = await _delegationRepository.GetAsync(id);
		await _scopeAccess.EnsureCanAccessAcademicYearAsync(entity.AcademicYearId, nameof(CancelDelegationAsync));
		entity.Cancel(); await _delegationRepository.UpdateAsync(entity, autoSave: true); return MapDelegation(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewAudit)]
	public async Task<PagedResultDto<AdmissionCrmTimelineEventDto>> SearchAuditAsync(GetAdmissionCrmAuditInput input)
	{
		await _scopeAccess.EnsureCanAccessAcademicYearAsync(input.AcademicYearId, nameof(SearchAuditAsync));
		var queryable = await _timelineRepository.GetQueryableAsync();
		var query = queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == input.AcademicYearId);
		if (input.LeadId.HasValue) query = query.Where(x => x.LeadId == input.LeadId);
		if (input.ActorUserId.HasValue) query = query.Where(x => x.ActorUserId == input.ActorUserId);
		if (input.EventType.HasValue) query = query.Where(x => x.EventType == input.EventType);
		var persistedItems = await AsyncExecuter.ToListAsync(query);
		var filteredItems = persistedItems
			.Where(x => !input.From.HasValue || x.OccurredAt >= input.From.Value)
			.Where(x => !input.To.HasValue || x.OccurredAt <= input.To.Value)
			.OrderByDescending(x => x.OccurredAt)
			.ThenByDescending(x => x.Id)
			.ToList();
		var items = filteredItems.Skip(input.SkipCount).Take(Math.Min(input.MaxResultCount, 500));
		return new PagedResultDto<AdmissionCrmTimelineEventDto>(filteredItems.Count, items.Select(MapTimeline).ToList());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ProtectedAnalytics)]
	public async Task<AdmissionCrmForecastDto> GetAdvisoryForecastAsync(Guid academicYearId)
	{
		await _scopeAccess.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(GetAdvisoryForecastAsync));
		var leadQuery = await _leadRepository.GetQueryableAsync();
		var leads = await AsyncExecuter.ToListAsync(leadQuery.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId));
		var offerQuery = await _offerRepository.GetQueryableAsync();
		var offers = await AsyncExecuter.ToListAsync(offerQuery.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId));
		var activationQuery = await _activationRepository.GetQueryableAsync();
		var activations = await AsyncExecuter.ToListAsync(activationQuery.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId));
		var responded = offers.Count(x => x.Status is AdmissionCrmSeatOfferStatus.Accepted or AdmissionCrmSeatOfferStatus.Declined);
		var accepted = offers.Count(x => x.Status == AdmissionCrmSeatOfferStatus.Accepted);
		var offerRate = responded == 0 ? 0m : (decimal)accepted / responded;
		var eligible = leads.Count(x => x.Stage >= AdmissionInquiryStage.Eligible && x.Stage is not (AdmissionInquiryStage.Rejected or AdmissionInquiryStage.Archived));
		var ready = activations.Count(x => x.Status == AdmissionCrmActivationStatus.FirstDayReady);
		var activationRate = eligible == 0 ? 0m : (decimal)ready / eligible;
		var open = leads.Count(x => x.Stage is not (AdmissionInquiryStage.Eligible or AdmissionInquiryStage.Rejected or AdmissionInquiryStage.Archived));
		return new AdmissionCrmForecastDto
		{
			AcademicYearId = academicYearId, GeneratedAt = Clock.Now, HistoricalLeadCount = leads.Count,
			CurrentOpenLeadCount = open, ObservedOfferAcceptanceRate = Math.Round(offerRate, 4),
			ObservedActivationRate = Math.Round(activationRate, 4),
			ProjectedAdditionalAcceptedOffers = (int)Math.Round(open * offerRate),
			ProjectedAdditionalActivations = (int)Math.Round(open * activationRate),
			Confidence = leads.Count >= 200 ? "Moderate" : leads.Count >= 50 ? "Low" : "Insufficient sample",
			Inputs = ["Current-cycle lead volume", "Observed issued-offer responses", "Observed eligible-to-activation conversion"],
			Limitations = ["Current-cycle descriptive projection only.", "No protected attributes are used.", "The projection cannot make or change an admission decision."],
			IsAdvisory = true
		};
	}

	private async Task<string> BuildExportContentAsync(Guid academicYearId, RequestAdmissionCrmExportDto input, DateTime at)
	{
		var rows = new List<string[]>
		{
			new[] { "Report", input.ReportType.ToString() },
			new[] { "Tenant", CurrentTenant.Id?.ToString() ?? "host" },
			new[] { "AcademicYearId", academicYearId.ToString() },
			new[] { "GeneratedAt", at.ToUniversalTime().ToString("O") },
			new[] { "DataAsOf", at.ToUniversalTime().ToString("O") },
			new[] { "DefinitionVersion", "1" },
			Array.Empty<string>()
		};
		if (input.ReportType == AdmissionCrmExportReportType.AuditTimeline)
		{
			rows.Add(["LeadId", "EventType", "Title", "ActorUserId", "OccurredAt", "Details"]);
			var queryable = await _timelineRepository.GetQueryableAsync();
			var persistedItems = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId));
			var items = persistedItems.OrderByDescending(x => x.OccurredAt).ThenByDescending(x => x.Id).Take(10_000);
			rows.AddRange(items.Select(x => new[] { x.LeadId.ToString(), x.EventType.ToString(), x.Title, x.ActorUserId?.ToString() ?? "", x.OccurredAt.ToString("O"), x.Details ?? "" }));
		}
		else if (input.ReportType == AdmissionCrmExportReportType.DataQuality)
		{
			rows.Add(["IssueId", "Severity", "Status", "IssueType", "LeadId", "DetectedAt", "Summary"]);
			var queryable = await _dataQualityRepository.GetQueryableAsync();
			var items = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId).Take(10_000));
			rows.AddRange(items.Select(x => new[] { x.Id.ToString(), x.Severity.ToString(), x.Status.ToString(), x.IssueType.ToString(), x.LeadId?.ToString() ?? "", x.LastDetectedAt.ToString("O"), x.Title }));
		}
		else if (input.ReportType == AdmissionCrmExportReportType.GovernmentReadiness)
		{
			rows.Add(["LeadId", "ApplicantPersonId", "ApplicantName", "DateOfBirth", "DesiredClass", "RouteType", "RouteStatus", "IdentityResolution", "CoreIdentityVerified", "PEN", "PENStatus", "APAAR", "APAARConsentStatus", "APAARStatus", "UDISEStudentCode", "UDISEStatus", "DataProvenance"]);
			var profileQueryable = await _identityProfileRepository.GetQueryableAsync();
			var profiles = await AsyncExecuter.ToListAsync(profileQueryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId));
			var personIds = profiles.Select(x => x.ApplicantPersonId).Distinct().ToList();
			var personQueryable = await _personRepository.GetQueryableAsync();
			var people = (await AsyncExecuter.ToListAsync(personQueryable.Where(x => x.TenantId == CurrentTenant.Id && personIds.Contains(x.Id)))).ToDictionary(x => x.Id);
			var leadIds = profiles.Select(x => x.LeadId).Distinct().ToList();
			var leadQueryable = await _leadRepository.GetQueryableAsync();
			var leads = (await AsyncExecuter.ToListAsync(leadQueryable.Where(x => x.TenantId == CurrentTenant.Id && leadIds.Contains(x.Id)))).ToDictionary(x => x.Id);
			var routeQueryable = await _routeRepository.GetQueryableAsync();
			var routes = (await AsyncExecuter.ToListAsync(routeQueryable.Where(x =>
				x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId && x.IsPrimary)))
				.GroupBy(x => x.LeadId)
				.ToDictionary(x => x.Key, x => x.OrderByDescending(route => route.CreationTime).First());
			foreach (var profile in profiles.OrderBy(x => x.LeadId))
			{
				if (!people.TryGetValue(profile.ApplicantPersonId, out var person) || !leads.TryGetValue(profile.LeadId, out var lead)) continue;
				routes.TryGetValue(profile.LeadId, out var route);
				rows.Add([
					lead.Id.ToString(), person.Id.ToString(), string.Join(" ", new[] { person.FirstName, person.MiddleName, person.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))),
					person.DateOfBirth?.ToString("yyyy-MM-dd") ?? "", lead.DesiredClassName ?? "", route?.RouteType.ToString() ?? "", route?.Status.ToString() ?? "",
					profile.Status.ToString(), person.CoreIdentityVerified.ToString(), person.PenNumber ?? "", person.PenStatus.ToString(), person.ApaarId ?? "",
					person.ApaarConsentStatus.ToString(), person.ApaarStatus.ToString(), person.UdiseStudentCode ?? "", person.UdiseStatus.ToString(), person.Provenance.ToString()
				]);
			}
		}
		else
		{
			rows.Add(["LeadId", "Stage", "Source", "DesiredClass", "OwnerUserId", "CreatedAt", "LastActivityAt"]);
			var queryable = await _leadRepository.GetQueryableAsync();
			var items = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId)
				.OrderBy(x => x.CreationTime).Take(10_000));
			rows.AddRange(items.Select(x => new[] { x.Id.ToString(), x.Stage.ToString(), x.InquirySourceSnapshot ?? "", x.DesiredClassName ?? "", x.OwnerUserId?.ToString() ?? "", x.CreationTime.ToString("O"), x.LastActivityTime.ToString("O") }));
		}
		return string.Join("\r\n", rows.Select(row => string.Join(",", row.Select(Csv))));
	}

	private static string Csv(string value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
	private Guid UserId() => CurrentUser.Id ?? throw new AbpAuthorizationException("Authenticated user required.");
	private async Task<AdmissionInquiryLead> GetLeadAsync(Guid id, string operation)
	{
		var lead = await _leadRepository.GetAsync(id); await _scopeAccess.EnsureCanAccessAcademicYearAsync(lead.AcademicYearId, operation); return lead;
	}
	private async Task<AdmissionCrmRetentionPolicy> GetRetentionAsync(Guid id)
	{
		var entity = await _retentionRepository.GetAsync(id); await _scopeAccess.EnsureCanAccessAcademicYearAsync(entity.AcademicYearId, nameof(GetRetentionAsync)); return entity;
	}
	private async Task<AdmissionCrmPrivacyRequest> GetPrivacyAsync(Guid id)
	{
		var entity = await _privacyRepository.GetAsync(id); await _scopeAccess.EnsureCanAccessAcademicYearAsync(entity.AcademicYearId, nameof(GetPrivacyAsync)); return entity;
	}
	private Task TimelineAsync(AdmissionInquiryLead lead, AdmissionCrmTimelineEventType type, string title, string detail, Guid relatedId) =>
		_timelineRepository.InsertAsync(new AdmissionCrmTimelineEvent(GuidGenerator.Create(), CurrentTenant.Id, lead.Id, lead.AcademicYearId, type, title, detail, relatedId, CurrentUser.Id, Clock.Now), autoSave: true);
	private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
	private static T? Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value, JsonOptions);

	private static AdmissionCrmRetentionPolicyDto MapRetention(AdmissionCrmRetentionPolicy x) => new()
	{
		Id = x.Id, AcademicYearId = x.AcademicYearId, Code = x.Code, Name = x.Name, Category = x.Category,
		Action = x.Action, RetainDays = x.RetainDays, Scope = Deserialize<Dictionary<string, string?>>(x.ScopeJson) ?? new(),
		AuthorityNote = x.AuthorityNote, Status = x.Status, SubmittedByUserId = x.SubmittedByUserId,
		ApprovedByUserId = x.ApprovedByUserId, PublishedAt = x.PublishedAt
	};
	private static AdmissionCrmPrivacyRequestDto MapPrivacy(AdmissionCrmPrivacyRequest x) => new()
	{
		Id = x.Id, AcademicYearId = x.AcademicYearId, LeadId = x.LeadId, Type = x.Type, Reason = x.Reason,
		Preview = Deserialize<AdmissionCrmPrivacyPreviewDto>(x.PreviewJson) ?? new(), Status = x.Status,
		RequestedByUserId = x.RequestedByUserId, RequestedAt = x.RequestedAt, DecidedByUserId = x.DecidedByUserId,
		DecidedAt = x.DecidedAt, DecisionReason = x.DecisionReason, CompletionReference = x.CompletionReference, CompletedAt = x.CompletedAt
	};
	private static AdmissionCrmExportRequestDto MapExport(AdmissionCrmExportRequest x) => new()
	{
		Id = x.Id, AcademicYearId = x.AcademicYearId, ReportType = x.ReportType, DefinitionVersion = x.DefinitionVersion,
		Filters = Deserialize<Dictionary<string, string?>>(x.FiltersJson) ?? new(), FileName = x.FileName, Status = x.Status,
		RequestedByUserId = x.RequestedByUserId, RequestedAt = x.RequestedAt, DataAsOf = x.DataAsOf,
		ExpiresAt = x.ExpiresAt, DownloadCount = x.DownloadCount, LastDownloadedAt = x.LastDownloadedAt
	};
	private static AdmissionCrmReportScheduleDto MapSchedule(AdmissionCrmReportSchedule x) => new()
	{
		Id = x.Id, AcademicYearId = x.AcademicYearId, Name = x.Name, ReportType = x.ReportType, Frequency = x.Frequency,
		Destination = x.Destination, Filters = Deserialize<Dictionary<string, string?>>(x.FiltersJson) ?? new(),
		NextRunAt = x.NextRunAt, IsActive = x.IsActive, LastRunAt = x.LastRunAt
	};
	private static AdmissionCrmWorkDelegationDto MapDelegation(AdmissionCrmWorkDelegation x) => new()
	{
		Id = x.Id, AcademicYearId = x.AcademicYearId, DelegatorUserId = x.DelegatorUserId, DelegateUserId = x.DelegateUserId,
		QueueTypes = Deserialize<IReadOnlyList<AdmissionCrmOperationsQueueType>>(x.QueueTypesJson) ?? [], StartsAt = x.StartsAt,
		EndsAt = x.EndsAt, Reason = x.Reason, Status = x.Status
	};
	private static AdmissionCrmTimelineEventDto MapTimeline(AdmissionCrmTimelineEvent x) => new()
	{
		Id = x.Id, LeadId = x.LeadId, AcademicYearId = x.AcademicYearId, EventType = x.EventType,
		Title = x.Title, Details = x.Details, RelatedEntityId = x.RelatedEntityId, ActorUserId = x.ActorUserId, OccurredAt = x.OccurredAt
	};
}

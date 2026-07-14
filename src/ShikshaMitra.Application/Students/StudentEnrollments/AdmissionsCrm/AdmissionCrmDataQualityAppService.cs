using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
public class AdmissionCrmDataQualityAppService : ApplicationService, IAdmissionCrmDataQualityAppService
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private readonly IRepository<AdmissionCrmDataQualityIssue, Guid> _issueRepository;
	private readonly IRepository<AdmissionInquiryLead, Guid> _leadRepository;
	private readonly IRepository<AdmissionCrmPerson, Guid> _personRepository;
	private readonly IRepository<AdmissionCrmSeatOffer, Guid> _offerRepository;
	private readonly IRepository<AdmissionCrmSeatReservation, Guid> _reservationRepository;
	private readonly IRepository<AdmissionCrmSeatPool, Guid> _seatPoolRepository;
	private readonly IRepository<AdmissionCrmActivation, Guid> _activationRepository;
	private readonly IRepository<AdmissionCrmIdentifierAllocation, Guid> _identifierRepository;
	private readonly IRepository<AdmissionCrmAdmissionQuotation, Guid> _quotationRepository;
	private readonly IRepository<AdmissionCrmTimelineEvent, Guid> _timelineRepository;
	private readonly IRepository<AdmissionCrmServiceHandoff, Guid> _serviceHandoffRepository;
	private readonly IRepository<AdmissionCrmPrivacyRequest, Guid> _privacyRepository;
	private readonly IRepository<AdmissionCrmExportRequest, Guid> _exportRepository;
	private readonly StudentScopeAccessService _studentScopeAccessService;

	public AdmissionCrmDataQualityAppService(
		IRepository<AdmissionCrmDataQualityIssue, Guid> issueRepository,
		IRepository<AdmissionInquiryLead, Guid> leadRepository,
		IRepository<AdmissionCrmPerson, Guid> personRepository,
		IRepository<AdmissionCrmSeatOffer, Guid> offerRepository,
		IRepository<AdmissionCrmSeatReservation, Guid> reservationRepository,
		IRepository<AdmissionCrmSeatPool, Guid> seatPoolRepository,
		IRepository<AdmissionCrmActivation, Guid> activationRepository,
		IRepository<AdmissionCrmIdentifierAllocation, Guid> identifierRepository,
		IRepository<AdmissionCrmAdmissionQuotation, Guid> quotationRepository,
		IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository,
		IRepository<AdmissionCrmServiceHandoff, Guid> serviceHandoffRepository,
		IRepository<AdmissionCrmPrivacyRequest, Guid> privacyRepository,
		IRepository<AdmissionCrmExportRequest, Guid> exportRepository,
		StudentScopeAccessService studentScopeAccessService)
	{
		_issueRepository = issueRepository; _leadRepository = leadRepository; _personRepository = personRepository;
		_offerRepository = offerRepository; _reservationRepository = reservationRepository; _seatPoolRepository = seatPoolRepository;
		_activationRepository = activationRepository; _identifierRepository = identifierRepository;
		_quotationRepository = quotationRepository; _timelineRepository = timelineRepository;
		_serviceHandoffRepository = serviceHandoffRepository; _privacyRepository = privacyRepository; _exportRepository = exportRepository;
		_studentScopeAccessService = studentScopeAccessService;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewDataQuality)]
	public async Task<PagedResultDto<AdmissionCrmDataQualityIssueDto>> GetListAsync(Guid academicYearId, GetAdmissionCrmDataQualityIssuesInput input)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(GetListAsync));
		var queryable = await _issueRepository.GetQueryableAsync();
		var query = queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId);
		if (input.Status.HasValue) query = query.Where(x => x.Status == input.Status.Value);
		if (input.MinimumSeverity.HasValue) query = query.Where(x => x.Severity >= input.MinimumSeverity.Value);
		if (input.IssueType.HasValue) query = query.Where(x => x.IssueType == input.IssueType.Value);
		if (input.LeadId.HasValue) query = query.Where(x => x.LeadId == input.LeadId.Value);
		var total = await AsyncExecuter.CountAsync(query);
		var entities = await AsyncExecuter.ToListAsync(query.OrderByDescending(x => x.Severity)
			.ThenByDescending(x => x.LastDetectedAt).Skip(input.SkipCount).Take(input.MaxResultCount));
		return new PagedResultDto<AdmissionCrmDataQualityIssueDto>(total, entities.Select(Map).ToList());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.RunReconciliation)]
	public async Task<AdmissionCrmReconciliationResultDto> RunAsync(Guid academicYearId)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(RunAsync));
		var now = Clock.Now;
		var detections = await DetectAsync(academicYearId, now);
		var issueQueryable = await _issueRepository.GetQueryableAsync();
		var existing = await AsyncExecuter.ToListAsync(issueQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId));
		var byFingerprint = existing.ToDictionary(x => x.Fingerprint);
		var newCount = 0;
		var reopenedCount = 0;
		foreach (var detection in detections)
		{
			if (byFingerprint.TryGetValue(detection.Fingerprint, out var issue))
			{
				var wasResolved = issue.Status == AdmissionCrmDataQualityIssueStatus.Resolved;
				issue.Redetect(detection.Severity, Serialize(detection.Details), now);
				await _issueRepository.UpdateAsync(issue);
				if (wasResolved) reopenedCount++;
			}
			else
			{
				issue = new AdmissionCrmDataQualityIssue(GuidGenerator.Create(), CurrentTenant.Id, academicYearId,
					detection.Fingerprint, detection.Type, detection.Severity, detection.LeadId, detection.StudentId,
					detection.Title, Serialize(detection.Details), Serialize(detection.ImpactedReports), now);
				await _issueRepository.InsertAsync(issue);
				byFingerprint.Add(issue.Fingerprint, issue);
				newCount++;
			}
		}
		var detectedFingerprints = detections.Select(x => x.Fingerprint).ToHashSet();
		var autoResolved = 0;
		foreach (var issue in existing.Where(x => !detectedFingerprints.Contains(x.Fingerprint) &&
			x.Status is AdmissionCrmDataQualityIssueStatus.Open or AdmissionCrmDataQualityIssueStatus.Acknowledged))
		{
			issue.ResolveByReconciliation(now);
			await _issueRepository.UpdateAsync(issue);
			autoResolved++;
		}
		await CurrentUnitOfWork.SaveChangesAsync();
		return new AdmissionCrmReconciliationResultDto
		{
			AcademicYearId = academicYearId, DataAsOf = now, DetectedCount = detections.Count,
			NewCount = newCount, ReopenedCount = reopenedCount, AutoResolvedCount = autoResolved,
			CountsByType = detections.GroupBy(x => x.Type.ToString()).ToDictionary(x => x.Key, x => x.Count())
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ResolveDataQuality)]
	public async Task<AdmissionCrmDataQualityIssueDto> AcknowledgeAsync(Guid id, AcknowledgeAdmissionCrmDataQualityIssueDto input)
	{
		var issue = await GetIssueAsync(id, nameof(AcknowledgeAsync));
		issue.Acknowledge(input.OwnerUserId ?? GetCurrentUserId());
		await _issueRepository.UpdateAsync(issue, autoSave: true);
		return Map(issue);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ResolveDataQuality)]
	public async Task<AdmissionCrmDataQualityIssueDto> ResolveAsync(Guid id, ResolveAdmissionCrmDataQualityIssueDto input)
	{
		var issue = await GetIssueAsync(id, nameof(ResolveAsync));
		issue.Resolve(input.Ignore, input.Reason, GetCurrentUserId(), Clock.Now);
		await _issueRepository.UpdateAsync(issue, autoSave: true);
		return Map(issue);
	}

	private async Task<List<Detection>> DetectAsync(Guid academicYearId, DateTime now)
	{
		var tenantId = CurrentTenant.Id;
		var leads = await ListAsync(_leadRepository, x => x.TenantId == tenantId && x.AcademicYearId == academicYearId);
		var people = await ListAsync(_personRepository, x => x.TenantId == tenantId);
		var offers = await ListAsync(_offerRepository, x => x.TenantId == tenantId && x.AcademicYearId == academicYearId);
		var reservations = await ListAsync(_reservationRepository, x => x.TenantId == tenantId && x.AcademicYearId == academicYearId);
		var pools = await ListAsync(_seatPoolRepository, x => x.TenantId == tenantId && x.AcademicYearId == academicYearId);
		var activations = await ListAsync(_activationRepository, x => x.TenantId == tenantId && x.AcademicYearId == academicYearId);
		var identifiers = await ListAsync(_identifierRepository, x => x.TenantId == tenantId && x.AcademicYearId == academicYearId);
		var quotations = await ListAsync(_quotationRepository, x => x.TenantId == tenantId && x.AcademicYearId == academicYearId);
		var timeline = await ListAsync(_timelineRepository, x => x.TenantId == tenantId && x.AcademicYearId == academicYearId);
		var serviceHandoffs = await ListAsync(_serviceHandoffRepository, x => x.TenantId == tenantId && x.AcademicYearId == academicYearId);
		var privacyRequests = await ListAsync(_privacyRepository, x => x.TenantId == tenantId && x.AcademicYearId == academicYearId);
		var exports = await ListAsync(_exportRepository, x => x.TenantId == tenantId && x.AcademicYearId == academicYearId);
		var detections = new List<Detection>();

		foreach (var group in leads.Where(x => x.MergedIntoLeadId == null && (x.NormalizedPrimaryContactNo != null || x.NormalizedEmail != null))
			.GroupBy(x => x.NormalizedPrimaryContactNo ?? x.NormalizedEmail!).Where(x => x.Count() > 1))
			Add(detections, AdmissionCrmDataQualityIssueType.DuplicateApplicant, AdmissionCrmDataQualityIssueSeverity.Warning,
				group.OrderBy(x => x.CreationTime).First().Id, null, "Possible duplicate applicants",
				new() { ["matchKey"] = group.Key, ["leadIds"] = string.Join(",", group.Select(x => x.Id)) }, ["Inquiry funnel", "Applicant count"]);

		foreach (var group in people.Where(x => x.NormalizedPhone != null || x.NormalizedEmail != null)
			.GroupBy(x => x.NormalizedPhone ?? x.NormalizedEmail!).Where(x => x.Count() > 1))
			Add(detections, AdmissionCrmDataQualityIssueType.DuplicatePerson, AdmissionCrmDataQualityIssueSeverity.Error,
				null, group.Select(x => x.StudentId).FirstOrDefault(x => x.HasValue), "Possible duplicate household people",
				new() { ["matchKey"] = group.Key, ["personIds"] = string.Join(",", group.Select(x => x.Id)) }, ["Household identity", "Student access"]);

		var reservationsById = reservations.ToDictionary(x => x.Id);
		foreach (var offer in offers.Where(x => x.Status == AdmissionCrmSeatOfferStatus.Accepted &&
			(!x.ReservationId.HasValue || !reservationsById.TryGetValue(x.ReservationId.Value, out var reservation) ||
			 reservation.Status is not (AdmissionCrmSeatReservationStatus.Active or AdmissionCrmSeatReservationStatus.Committed))))
			Add(detections, AdmissionCrmDataQualityIssueType.AcceptedOfferWithoutReservation, AdmissionCrmDataQualityIssueSeverity.Critical,
				offer.LeadId, null, "Accepted offer has no active seat reservation", new() { ["offerId"] = offer.Id.ToString(), ["offerNumber"] = offer.OfferNumber }, ["Seat occupancy", "Offer yield"]);

		var leadsById = leads.ToDictionary(x => x.Id);
		foreach (var activation in activations.Where(x => x.Status == AdmissionCrmActivationStatus.FirstDayReady &&
			(!x.StudentId.HasValue || !leadsById.TryGetValue(x.LeadId, out var lead) || lead.StudentId != x.StudentId)))
			Add(detections, AdmissionCrmDataQualityIssueType.ActivatedStudentWithoutLineage, AdmissionCrmDataQualityIssueSeverity.Critical,
				activation.LeadId, activation.StudentId, "Activated student is missing enrollment lineage", new() { ["activationId"] = activation.Id.ToString() }, ["Activation yield", "Student count"]);

		foreach (var group in identifiers.Where(x => x.Status == AdmissionCrmIdentifierStatus.Committed)
			.GroupBy(x => new { x.IdentifierType, x.Value }).Where(x => x.Count() > 1))
			Add(detections, AdmissionCrmDataQualityIssueType.DuplicateIdentifier, AdmissionCrmDataQualityIssueSeverity.Critical,
				group.First().LeadId, group.First().StudentId, "Duplicate committed student identifier", new() { ["identifier"] = group.Key.Value, ["allocationIds"] = string.Join(",", group.Select(x => x.Id)) }, ["GR register", "Student identity"]);

		foreach (var activation in activations.Where(x => x.Status == AdmissionCrmActivationStatus.FirstDayReady &&
			(!x.FirstDayReadyAt.HasValue || !x.StudentId.HasValue)))
			Add(detections, AdmissionCrmDataQualityIssueType.ImpossibleActivationState, AdmissionCrmDataQualityIssueSeverity.Critical,
				activation.LeadId, activation.StudentId, "Activation state is internally inconsistent", new() { ["activationId"] = activation.Id.ToString() }, ["Activation funnel"]);

		var committedIdentifiersByActivation = identifiers.Where(x => x.Status == AdmissionCrmIdentifierStatus.Committed)
			.Select(x => x.ActivationId).ToHashSet();
		var acceptedOffersByLead = offers.Where(x => x.Status == AdmissionCrmSeatOfferStatus.Accepted)
			.Select(x => x.LeadId).ToHashSet();
		foreach (var activation in activations.Where(x => x.Status == AdmissionCrmActivationStatus.Cancelled &&
			(!x.CancelledAt.HasValue || string.IsNullOrWhiteSpace(x.CancellationReason) ||
			 committedIdentifiersByActivation.Contains(x.Id) || acceptedOffersByLead.Contains(x.LeadId))))
			Add(detections, AdmissionCrmDataQualityIssueType.ImpossibleActivationState, AdmissionCrmDataQualityIssueSeverity.Critical,
				activation.LeadId, activation.StudentId, "Cancelled activation has unresolved downstream state",
				new() { ["activationId"] = activation.Id.ToString(), ["committedIdentifier"] = committedIdentifiersByActivation.Contains(activation.Id).ToString(), ["acceptedOffer"] = acceptedOffersByLead.Contains(activation.LeadId).ToString() },
				["Activation cancellation", "Seat occupancy", "GR register"]);

		foreach (var quotation in quotations.Where(x => x.Status == AdmissionCrmQuotationStatus.Issued &&
			x.ValidUntil < now && !x.IsGateSatisfiedAt(now)))
			Add(detections, AdmissionCrmDataQualityIssueType.StalePaymentState, AdmissionCrmDataQualityIssueSeverity.Warning,
				quotation.LeadId, null, "Expired quotation still has a pending payment state", new() { ["quotationId"] = quotation.Id.ToString(), ["quotationNumber"] = quotation.QuotationNumber }, ["Payment conversion", "Financial readiness"]);

		var offerIds = offers.Select(x => x.Id).ToHashSet();
		var poolIds = pools.Select(x => x.Id).ToHashSet();
		foreach (var reservation in reservations.Where(x => !leadsById.ContainsKey(x.LeadId) || !offerIds.Contains(x.SeatOfferId) || !poolIds.Contains(x.SeatPoolId)))
			Add(detections, AdmissionCrmDataQualityIssueType.OrphanedSeatReservation, AdmissionCrmDataQualityIssueSeverity.Critical,
				reservation.LeadId, null, "Seat reservation references a missing enrollment record", new() { ["reservationId"] = reservation.Id.ToString() }, ["Seat occupancy", "Remaining capacity"]);

		var activatedTimelineIds = timeline.Where(x => x.EventType == AdmissionCrmTimelineEventType.StudentActivated && x.RelatedEntityId.HasValue)
			.Select(x => x.RelatedEntityId!.Value).ToHashSet();
		foreach (var activation in activations.Where(x => x.Status == AdmissionCrmActivationStatus.FirstDayReady && !activatedTimelineIds.Contains(x.Id)))
			Add(detections, AdmissionCrmDataQualityIssueType.MissingAuditReference, AdmissionCrmDataQualityIssueSeverity.Error,
					activation.LeadId, activation.StudentId, "Completed activation is missing its audit event", new() { ["activationId"] = activation.Id.ToString() }, ["Audit export", "Activation funnel"]);

		foreach (var handoff in serviceHandoffs.Where(x => x.Status == AdmissionCrmServiceHandoffStatus.Failed))
			Add(detections, AdmissionCrmDataQualityIssueType.ServiceHandoffFailure, AdmissionCrmDataQualityIssueSeverity.Warning,
				handoff.LeadId, handoff.StudentId, "Optional service handoff requires attention",
				new() { ["handoffId"] = handoff.Id.ToString(), ["service"] = handoff.Type.ToString(), ["error"] = handoff.LastError }, ["Service handoff", "First-day readiness exceptions"]);

		foreach (var privacy in privacyRequests.Where(x => x.Status == AdmissionCrmPrivacyRequestStatus.Approved &&
			x.DecidedAt.HasValue && x.DecidedAt.Value < now.AddDays(-7)))
			Add(detections, AdmissionCrmDataQualityIssueType.StalePrivacyRequest, AdmissionCrmDataQualityIssueSeverity.Error,
				privacy.LeadId, null, "Approved privacy request has not been completed",
				new() { ["privacyRequestId"] = privacy.Id.ToString(), ["approvedAt"] = privacy.DecidedAt?.ToString("O") }, ["Privacy operations", "Retention audit"]);

		foreach (var export in exports.Where(x => x.ExpiresAt < now && x.Content != "Expired and removed."))
			Add(detections, AdmissionCrmDataQualityIssueType.ExpiredSensitiveExportContent, AdmissionCrmDataQualityIssueSeverity.Critical,
				null, null, "Expired sensitive export content has not been removed",
				new() { ["exportId"] = export.Id.ToString(), ["expiredAt"] = export.ExpiresAt.ToString("O") }, ["Sensitive exports", "Retention audit"]);

		return detections;
	}

	private async Task<List<T>> ListAsync<T>(IRepository<T, Guid> repository, System.Linq.Expressions.Expression<Func<T, bool>> predicate) where T : class, Volo.Abp.Domain.Entities.IEntity<Guid>
	{
		var queryable = await repository.GetQueryableAsync();
		return await AsyncExecuter.ToListAsync(queryable.Where(predicate));
	}

	private static void Add(List<Detection> target, AdmissionCrmDataQualityIssueType type,
		AdmissionCrmDataQualityIssueSeverity severity, Guid? leadId, Guid? studentId, string title,
		Dictionary<string, string?> details, IReadOnlyList<string> reports)
	{
		var identity = $"{type}|{leadId}|{studentId}|{string.Join("|", details.OrderBy(x => x.Key).Select(x => $"{x.Key}={x.Value}"))}";
		target.Add(new Detection(Hash(identity), type, severity, leadId, studentId, title, details, reports));
	}

	private async Task<AdmissionCrmDataQualityIssue> GetIssueAsync(Guid id, string operation)
	{
		var issue = await _issueRepository.GetAsync(id);
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(issue.AcademicYearId, operation);
		return issue;
	}

	private static AdmissionCrmDataQualityIssueDto Map(AdmissionCrmDataQualityIssue x) => new()
	{
		Id = x.Id, AcademicYearId = x.AcademicYearId, IssueType = x.IssueType, Severity = x.Severity,
		Status = x.Status, LeadId = x.LeadId, StudentId = x.StudentId, Title = x.Title,
		Details = Deserialize<Dictionary<string, string?>>(x.DetailsJson) ?? new(),
		ImpactedReports = Deserialize<IReadOnlyList<string>>(x.ImpactedReportsJson) ?? [],
		FirstDetectedAt = x.FirstDetectedAt, LastDetectedAt = x.LastDetectedAt,
		OwnerUserId = x.OwnerUserId, ResolutionReason = x.ResolutionReason, ResolvedAt = x.ResolvedAt
	};

	private Guid GetCurrentUserId() => CurrentUser.Id ?? throw new AbpAuthorizationException("An authenticated user is required.");
	private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
	private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
	private static T? Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value, JsonOptions);
	private sealed record Detection(string Fingerprint, AdmissionCrmDataQualityIssueType Type,
		AdmissionCrmDataQualityIssueSeverity Severity, Guid? LeadId, Guid? StudentId, string Title,
		Dictionary<string, string?> Details, IReadOnlyList<string> ImpactedReports);
}

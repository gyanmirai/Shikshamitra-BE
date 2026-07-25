using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmSpecialRouteAppService : ApplicationService, IAdmissionCrmSpecialRouteAppService
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private readonly IRepository<AdmissionCrmRouteDefinition, Guid> _definitionRepository;
	private readonly IRepository<AdmissionCrmRouteAssignment, Guid> _assignmentRepository;
	private readonly IRepository<AdmissionCrmRouteAppeal, Guid> _appealRepository;
	private readonly IRepository<AdmissionInquiryLead, Guid> _leadRepository;
	private readonly IRepository<AdmissionCrmSeatOffer, Guid> _offerRepository;
	private readonly IRepository<AdmissionCrmAdmissionQuotation, Guid> _quotationRepository;
	private readonly IRepository<AdmissionCrmDocumentChecklistAssignment, Guid> _documentRepository;
	private readonly IStudentRepository _studentRepository;
	private readonly IRepository<AdmissionCrmTimelineEvent, Guid> _timelineRepository;
	private readonly StudentScopeAccessService _studentScopeAccessService;

	public AdmissionCrmSpecialRouteAppService(IRepository<AdmissionCrmRouteDefinition, Guid> definitionRepository,
		IRepository<AdmissionCrmRouteAssignment, Guid> assignmentRepository,
		IRepository<AdmissionCrmRouteAppeal, Guid> appealRepository,
		IRepository<AdmissionInquiryLead, Guid> leadRepository,
		IRepository<AdmissionCrmSeatOffer, Guid> offerRepository,
		IRepository<AdmissionCrmAdmissionQuotation, Guid> quotationRepository,
		IRepository<AdmissionCrmDocumentChecklistAssignment, Guid> documentRepository,
		IStudentRepository studentRepository,
		IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository,
		StudentScopeAccessService studentScopeAccessService)
	{
		_definitionRepository = definitionRepository; _assignmentRepository = assignmentRepository;
		_appealRepository = appealRepository;
		_leadRepository = leadRepository; _studentRepository = studentRepository;
		_offerRepository = offerRepository; _quotationRepository = quotationRepository;
		_documentRepository = documentRepository;
		_timelineRepository = timelineRepository; _studentScopeAccessService = studentScopeAccessService;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageAdmissionRoutes)]
	public async Task<AdmissionCrmRouteDefinitionDto> CreateDefinitionAsync(Guid academicYearId, CreateAdmissionCrmRouteDefinitionDto input)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(CreateDefinitionAsync));
		var queryable = await _definitionRepository.GetQueryableAsync();
		var existing = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId && x.Code == input.Code.ToUpper()));
		var version = existing.Count == 0 ? 1 : existing.Max(x => x.Version) + 1;
		var entity = new AdmissionCrmRouteDefinition(GuidGenerator.Create(), CurrentTenant.Id, academicYearId,
			input.Code, input.Name, input.RouteType, version, Serialize(input.Configuration), input.EffectiveFrom, input.EffectiveUntil);
		await _definitionRepository.InsertAsync(entity, autoSave: true);
		return MapDefinition(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewAdmissionRoutes)]
	public async Task<IReadOnlyList<AdmissionCrmRouteDefinitionDto>> GetDefinitionsAsync(Guid academicYearId)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(GetDefinitionsAsync));
		var queryable = await _definitionRepository.GetQueryableAsync();
		var entities = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId));
		return entities.OrderBy(x => x.RouteType).ThenByDescending(x => x.Version).Select(MapDefinition).ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageAdmissionRoutes)]
	public async Task<AdmissionCrmRouteDefinitionDto> SubmitDefinitionAsync(Guid id)
	{
		var entity = await GetDefinitionAsync(id); entity.Submit(GetCurrentUserId());
		await _definitionRepository.UpdateAsync(entity, autoSave: true); return MapDefinition(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ApproveAdmissionRoutes)]
	public async Task<AdmissionCrmRouteDefinitionDto> PublishDefinitionAsync(Guid id)
	{
		var entity = await GetDefinitionAsync(id); entity.Publish(GetCurrentUserId());
		var queryable = await _definitionRepository.GetQueryableAsync();
		var prior = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == entity.AcademicYearId && x.Code == entity.Code && x.Id != entity.Id && x.Status == AdmissionCrmRouteDefinitionStatus.Published));
		foreach (var item in prior) { item.Retire(); await _definitionRepository.UpdateAsync(item); }
		await _definitionRepository.UpdateAsync(entity, autoSave: true); return MapDefinition(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.AssignAdmissionRoute)]
	public async Task<AdmissionCrmRouteAssignmentDto> AssignAsync(Guid leadId, AssignAdmissionCrmSpecialRouteDto input)
	{
		var lead = await GetLeadAsync(leadId, nameof(AssignAsync));
		var definition = await GetDefinitionAsync(input.RouteDefinitionId);
		var queryable = await _assignmentRepository.GetQueryableAsync();
		var current = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.LeadId == lead.Id &&
			x.IsPrimary));
		EnsureDefinitionCanBeAssigned(definition, lead);
		await ValidateRouteDataAsync(definition.RouteType, input.RouteData, lead);
		AdmissionCrmRouteEvidenceValidator.EnsureValid(definition, input.EvidenceReferences);
		if (current != null && string.IsNullOrWhiteSpace(input.Reason)) throw new BusinessException("AdmissionCrm:RouteChangeReasonRequired");
		current?.Supersede();
		if (current != null) await _assignmentRepository.UpdateAsync(current);
		var entity = new AdmissionCrmRouteAssignment(GuidGenerator.Create(), CurrentTenant.Id, lead.AcademicYearId,
			lead.Id, definition.Id, definition.Version, definition.RouteType, Serialize(input.RouteData),
			Serialize(input.EvidenceReferences), current?.Id, input.Reason, GetCurrentUserId(), Clock.Now);
		await _assignmentRepository.InsertAsync(entity, autoSave: true);
		await AddTimelineAsync(lead, current == null ? AdmissionCrmTimelineEventType.AdmissionRouteAssigned : AdmissionCrmTimelineEventType.AdmissionRouteChanged,
			current == null ? "Admission route assigned" : "Admission route changed", $"{definition.Name}: {input.Reason}", entity.Id);
		return MapAssignment(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.AssignAdmissionRoute)]
	public async Task<AdmissionCrmRouteImpactPreviewDto> PreviewAssignmentAsync(Guid leadId, AssignAdmissionCrmSpecialRouteDto input)
	{
		var lead = await GetLeadAsync(leadId, nameof(PreviewAssignmentAsync));
		var definition = await GetDefinitionAsync(input.RouteDefinitionId);
		var assignmentQuery = await _assignmentRepository.GetQueryableAsync();
		var current = await AsyncExecuter.FirstOrDefaultAsync(assignmentQuery.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.LeadId == leadId &&
			x.IsPrimary));
		EnsureDefinitionCanBeAssigned(definition, lead);
		await ValidateRouteDataAsync(definition.RouteType, input.RouteData, lead);
		AdmissionCrmRouteEvidenceValidator.EnsureValid(definition, input.EvidenceReferences);

		var impacts = new List<AdmissionCrmRouteImpactItemDto>();
		void Impact(string area, string detail, bool blocking = false) => impacts.Add(new()
			{ Area = area, Impact = detail, RequiresResolution = blocking });

		if (current != null && current.RouteDefinitionId != definition.Id)
			Impact("Route", $"{current.RouteType} version {current.RouteDefinitionVersion} will be superseded by {definition.RouteType} version {definition.Version}.");

		var offerQuery = await _offerRepository.GetQueryableAsync();
		var openOffers = await AsyncExecuter.ToListAsync(offerQuery.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == leadId &&
			x.Status != AdmissionCrmSeatOfferStatus.Declined && x.Status != AdmissionCrmSeatOfferStatus.Expired &&
			x.Status != AdmissionCrmSeatOfferStatus.Revoked && x.Status != AdmissionCrmSeatOfferStatus.Cancelled &&
			x.Status != AdmissionCrmSeatOfferStatus.Superseded));
		if (openOffers.Count > 0) Impact("Seat offer", $"{openOffers.Count} active offer(s) require review before the route change is finalized.", true);

		var quoteQuery = await _quotationRepository.GetQueryableAsync();
		var activeQuotes = await AsyncExecuter.ToListAsync(quoteQuery.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == leadId && x.Status == AdmissionCrmQuotationStatus.Issued));
		if (activeQuotes.Count > 0) Impact("Fees", "The active admission quotation must be reviewed against the target route fee policy.", true);

		var documentQuery = await _documentRepository.GetQueryableAsync();
		var checklistCount = await AsyncExecuter.CountAsync(documentQuery.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == leadId));
		if (checklistCount > 0) Impact("Documents", "The assigned document checklist must be recalculated for the target route.");

		var targetConfiguration = Deserialize<AdmissionCrmRouteConfigurationDto>(definition.ConfigurationJson) ?? new();
		if (targetConfiguration.RequiresPolicyEvaluation) Impact("Policy", "Route eligibility and applicable policy rules must be evaluated for the target route.", true);
		if (!string.IsNullOrWhiteSpace(targetConfiguration.SeatPoolCode)) Impact("Seat pool", $"Target route expects seat pool {targetConfiguration.SeatPoolCode}.");
		if (targetConfiguration.DecisionSlaHours.HasValue) Impact("Deadline", $"The route decision SLA will be {targetConfiguration.DecisionSlaHours} hour(s).");
		if (impacts.Count == 0) Impact("Route", "No downstream admission artifacts require recalculation.");

		return new AdmissionCrmRouteImpactPreviewDto
		{
			LeadId = leadId, CurrentRoute = current == null ? null : MapAssignment(current),
			TargetRoute = MapDefinition(definition), Impacts = impacts,
			HasBlockingImpact = impacts.Any(x => x.RequiresResolution)
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewAdmissionRoutes)]
	public async Task<IReadOnlyList<AdmissionCrmRouteAssignmentDto>> GetHistoryAsync(Guid leadId)
	{
		await GetLeadAsync(leadId, nameof(GetHistoryAsync));
		var queryable = await _assignmentRepository.GetQueryableAsync();
		var entities = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == leadId));
		return entities.OrderByDescending(x => x.AssignedAt).Select(MapAssignment).ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.EvaluateAdmissionRoute)]
	public async Task<AdmissionCrmRouteAssignmentDto> EvaluateAsync(Guid id, EvaluateAdmissionCrmSpecialRouteDto input)
	{
		var entity = await _assignmentRepository.GetAsync(id);
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(entity.AcademicYearId, nameof(EvaluateAsync));
		var definition = await GetDefinitionAsync(entity.RouteDefinitionId);
		if (definition.AcademicYearId != entity.AcademicYearId ||
		    definition.Version != entity.RouteDefinitionVersion ||
		    definition.RouteType != entity.RouteType)
		{
			throw new BusinessException("AdmissionCrm:RouteDefinitionSnapshotMismatch")
				.WithData("RouteAssignmentId", entity.Id)
				.WithData("RouteDefinitionId", definition.Id);
		}
		AdmissionCrmRouteEvidenceValidator.EnsureValid(
			definition,
			Deserialize<IReadOnlyList<string>>(entity.EvidenceJson) ?? []);
		if (entity.AssignedByUserId == GetCurrentUserId() && input.Status is AdmissionCrmRouteEligibilityStatus.Eligible or AdmissionCrmRouteEligibilityStatus.ExceptionApproved)
			throw new BusinessException("AdmissionCrm:DistinctRouteEligibilityApproverRequired");
		entity.Evaluate(input.Status, input.Reason, GetCurrentUserId(), Clock.Now);
		await _assignmentRepository.UpdateAsync(entity, autoSave: true);
		var lead = await GetLeadAsync(entity.LeadId, nameof(EvaluateAsync));
		await AddTimelineAsync(lead, AdmissionCrmTimelineEventType.AdmissionRouteEvaluated,
			"Admission route evaluated", $"{input.Status}: {input.Reason}", entity.Id);
		return MapAssignment(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageGovernmentReadiness)]
	public async Task<AdmissionCrmRteAllotmentImportResultDto> PreviewRteAllotmentsAsync(
		Guid academicYearId,
		ImportAdmissionCrmRteAllotmentsDto input)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(PreviewRteAllotmentsAsync));
		var results = new List<AdmissionCrmRteAllotmentImportItemResultDto>();
		var batchNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var row in input.Rows)
		{
			var item = new AdmissionCrmRteAllotmentImportItemResultDto
			{
				LeadId = row.LeadId,
				ApplicantReference = row.ApplicantReference,
				AllotmentNumber = row.RouteData.AllotmentNumber
			};
			try
			{
				var normalizedNumber = row.RouteData.AllotmentNumber?.Trim();
				if (string.IsNullOrWhiteSpace(normalizedNumber))
					throw new BusinessException("AdmissionCrm:RteAllotmentNumberRequired");
				if (!batchNumbers.Add(normalizedNumber))
					throw new BusinessException("AdmissionCrm:DuplicateRteAllotmentInImport");
				var lead = await ResolveImportLeadAsync(academicYearId, row, nameof(PreviewRteAllotmentsAsync));
				item.LeadId = lead.Id;
				if (lead.AcademicYearId != academicYearId)
					throw new BusinessException("AdmissionCrm:RteAllotmentCycleMismatch");
				var definition = await GetDefinitionAsync(row.RouteDefinitionId);
				if (definition.AcademicYearId != academicYearId || definition.RouteType != AdmissionCrmRouteType.RteEws)
					throw new BusinessException("AdmissionCrm:RteRouteDefinitionRequired");
				EnsureDefinitionCanBeAssigned(definition, lead);
				await ValidateRouteDataAsync(definition.RouteType, row.RouteData, lead);
				AdmissionCrmRouteEvidenceValidator.EnsureValid(definition, row.EvidenceReferences);
				item.Success = true;
			}
			catch (BusinessException exception)
			{
				item.ErrorCode = exception.Code ?? exception.Message;
			}
			results.Add(item);
		}

		return new AdmissionCrmRteAllotmentImportResultDto
		{
			Succeeded = results.Count(item => item.Success),
			Failed = results.Count(item => !item.Success),
			Items = results
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageGovernmentReadiness)]
	public async Task<AdmissionCrmRteAllotmentImportResultDto> ImportRteAllotmentsAsync(
		Guid academicYearId,
		ImportAdmissionCrmRteAllotmentsDto input)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(ImportRteAllotmentsAsync));
		var results = new List<AdmissionCrmRteAllotmentImportItemResultDto>();
		var batchNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var row in input.Rows)
		{
			var item = new AdmissionCrmRteAllotmentImportItemResultDto
			{
				LeadId = row.LeadId,
				ApplicantReference = row.ApplicantReference,
				AllotmentNumber = row.RouteData.AllotmentNumber
			};
			try
			{
				var normalizedNumber = row.RouteData.AllotmentNumber?.Trim();
				if (string.IsNullOrWhiteSpace(normalizedNumber) || !batchNumbers.Add(normalizedNumber))
					throw new BusinessException("AdmissionCrm:DuplicateRteAllotmentInImport");
				var lead = await ResolveImportLeadAsync(academicYearId, row, nameof(ImportRteAllotmentsAsync));
				item.LeadId = lead.Id;
				if (lead.AcademicYearId != academicYearId) throw new BusinessException("AdmissionCrm:RteAllotmentCycleMismatch");
				var assignment = await AssignAsync(lead.Id, new AssignAdmissionCrmSpecialRouteDto
				{
					RouteDefinitionId = row.RouteDefinitionId, RouteData = row.RouteData,
					EvidenceReferences = row.EvidenceReferences,
					Reason = $"Government allotment import: {normalizedNumber}."
				});
				item.Success = true; item.RouteAssignmentId = assignment.Id;
				await AddTimelineAsync(lead, AdmissionCrmTimelineEventType.GovernmentAllotmentImported,
					"Government allotment imported", normalizedNumber, assignment.Id);
			}
			catch (BusinessException exception)
			{
				item.ErrorCode = exception.Code ?? exception.Message;
			}
			results.Add(item);
		}
		return new AdmissionCrmRteAllotmentImportResultDto
		{
			Succeeded = results.Count(x => x.Success), Failed = results.Count(x => !x.Success), Items = results
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRouteAppeals)]
	public async Task<AdmissionCrmRouteAppealDto> RequestAppealAsync(Guid assignmentId, RequestAdmissionCrmRouteAppealDto input)
	{
		var assignment = await _assignmentRepository.GetAsync(assignmentId);
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(assignment.AcademicYearId, nameof(RequestAppealAsync));
		var appealQuery = await _appealRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(appealQuery.Where(x => x.TenantId == CurrentTenant.Id &&
			x.RouteAssignmentId == assignmentId && (x.Status == AdmissionCrmRouteAppealStatus.Requested || x.Status == AdmissionCrmRouteAppealStatus.UnderReview))))
			throw new BusinessException("AdmissionCrm:PendingRouteAppealAlreadyExists");
		var entity = new AdmissionCrmRouteAppeal(GuidGenerator.Create(), CurrentTenant.Id, assignment.AcademicYearId,
			assignment.LeadId, assignment.Id, assignment.Status, input.RequestedStatus, input.Reason,
			Serialize(input.EvidenceReferences), GetCurrentUserId(), Clock.Now);
		await _appealRepository.InsertAsync(entity, autoSave: true);
		var lead = await GetLeadAsync(assignment.LeadId, nameof(RequestAppealAsync));
		await AddTimelineAsync(lead, AdmissionCrmTimelineEventType.AdmissionRouteAppealed,
			"Admission route appeal requested", input.Reason, entity.Id);
		return MapAppeal(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ApproveRouteAppeals)]
	public async Task<AdmissionCrmRouteAppealDto> DecideAppealAsync(Guid appealId, DecideAdmissionCrmRouteAppealDto input)
	{
		var entity = await _appealRepository.GetAsync(appealId);
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(entity.AcademicYearId, nameof(DecideAppealAsync));
		entity.Decide(input.Approved, input.Reason, GetCurrentUserId(), Clock.Now);
		await _appealRepository.UpdateAsync(entity, autoSave: true);
		var lead = await GetLeadAsync(entity.LeadId, nameof(DecideAppealAsync));
		await AddTimelineAsync(lead, AdmissionCrmTimelineEventType.AdmissionRouteAppealDecided,
			"Admission route appeal decided", $"{entity.Status}: {input.Reason}", entity.Id);
		return MapAppeal(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewAdmissionRoutes)]
	public async Task<IReadOnlyList<AdmissionCrmRouteAppealDto>> GetAppealsAsync(Guid leadId)
	{
		await GetLeadAsync(leadId, nameof(GetAppealsAsync));
		var queryable = await _appealRepository.GetQueryableAsync();
		var entities = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == leadId));
		return entities.OrderByDescending(x => x.RequestedAt).Select(MapAppeal).ToList();
	}

	private async Task ValidateRouteDataAsync(
		AdmissionCrmRouteType type,
		AdmissionCrmSpecialRouteDataDto data,
		AdmissionInquiryLead lead)
	{
		if (type == AdmissionCrmRouteType.SiblingPriority && !data.RelatedStudentId.HasValue)
			throw new BusinessException("AdmissionCrm:VerifiedSiblingStudentRequired");
		if (type == AdmissionCrmRouteType.ReturningStudent && !data.FormerStudentId.HasValue)
			throw new BusinessException("AdmissionCrm:FormerStudentRequired");
		if (data.RelatedStudentId.HasValue)
		{
			if (type == AdmissionCrmRouteType.SiblingPriority)
				await EnsureSiblingRelationshipCanBeVerifiedAsync(lead, data.RelatedStudentId.Value);
			else
				await _studentRepository.GetAsync(data.RelatedStudentId.Value);
		}
		if (data.FormerStudentId.HasValue) await _studentRepository.GetAsync(data.FormerStudentId.Value);
		if (type == AdmissionCrmRouteType.StaffChild && string.IsNullOrWhiteSpace(data.AuthoritativeReference))
			throw new BusinessException("AdmissionCrm:AuthoritativeStaffReferenceRequired");
		if (type == AdmissionCrmRouteType.RteEws && (string.IsNullOrWhiteSpace(data.AuthorityName) ||
			string.IsNullOrWhiteSpace(data.AllotmentNumber) || string.IsNullOrWhiteSpace(data.AllotmentRound) || !data.AdmissionDeadline.HasValue))
			throw new BusinessException("AdmissionCrm:RteAllotmentDetailsRequired");
		if (type == AdmissionCrmRouteType.RteEws)
		{
			var assignmentQuery = await _assignmentRepository.GetQueryableAsync();
			var rteAssignments = await AsyncExecuter.ToListAsync(assignmentQuery.Where(x => x.TenantId == CurrentTenant.Id && x.RouteType == AdmissionCrmRouteType.RteEws));
			if (rteAssignments.Any(x => string.Equals(
				Deserialize<AdmissionCrmSpecialRouteDataDto>(x.RouteDataJson)?.AllotmentNumber,
				data.AllotmentNumber, StringComparison.OrdinalIgnoreCase)))
				throw new BusinessException("AdmissionCrm:RteAllotmentAlreadyImported");
		}
		if (type is AdmissionCrmRouteType.Transfer or AdmissionCrmRouteType.Lateral or AdmissionCrmRouteType.Interstate &&
			(string.IsNullOrWhiteSpace(data.PreviousSchool) || string.IsNullOrWhiteSpace(data.PreviousBoard) || string.IsNullOrWhiteSpace(data.LastClassCompleted)))
			throw new BusinessException("AdmissionCrm:TransferHistoryRequired");
		if (type == AdmissionCrmRouteType.OverseasReturn && (string.IsNullOrWhiteSpace(data.Country) || string.IsNullOrWhiteSpace(data.EquivalenceStatus)))
			throw new BusinessException("AdmissionCrm:OverseasEquivalenceDetailsRequired");
	}

	private async Task EnsureSiblingRelationshipCanBeVerifiedAsync(
		AdmissionInquiryLead lead,
		Guid relatedStudentId)
	{
		if (lead.StudentId == relatedStudentId)
		{
			throw new BusinessException("AdmissionCrm:SiblingRelationshipNotVerified")
				.WithData("RelatedStudentId", relatedStudentId);
		}

		var relatedStudent = await _studentRepository.GetInquiryForUpdateAsync(relatedStudentId);
		if (relatedStudent == null || relatedStudent.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException("AdmissionCrm:VerifiedSiblingStudentRequired")
				.WithData("RelatedStudentId", relatedStudentId);
		}

		var leadPhone = NormalizeContact(lead.PrimaryContactNo);
		var leadEmail = NormalizeEmail(lead.Email);
		var relatedPhones = new[]
			{
				relatedStudent.ContactDetail?.PrimaryContactNo,
				relatedStudent.ContactDetail?.SecondaryContactNo,
				relatedStudent.FamilyDetail?.FatherDetails?.ContactNo,
				relatedStudent.FamilyDetail?.MotherDetails?.ContactNo,
				relatedStudent.FamilyDetail?.GuardianDetails?.ContactNo
			}
			.Select(NormalizeContact)
			.Where(value => value != null)
			.ToHashSet(StringComparer.Ordinal);
		var relatedEmails = new[]
			{
				relatedStudent.ContactDetail?.Email,
				relatedStudent.FamilyDetail?.FatherDetails?.Email,
				relatedStudent.FamilyDetail?.MotherDetails?.Email,
				relatedStudent.FamilyDetail?.GuardianDetails?.Email
			}
			.Select(NormalizeEmail)
			.Where(value => value != null)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		if ((leadPhone == null || !relatedPhones.Contains(leadPhone)) &&
		    (leadEmail == null || !relatedEmails.Contains(leadEmail)))
		{
			throw new BusinessException("AdmissionCrm:SiblingRelationshipNotVerified")
				.WithData("RelatedStudentId", relatedStudentId);
		}
	}

	private static string? NormalizeContact(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}

		var digits = new string(value.Where(char.IsDigit).ToArray());
		return digits.Length == 0 ? null : digits;
	}

	private static string? NormalizeEmail(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

	private async Task<AdmissionCrmRouteDefinition> GetDefinitionAsync(Guid id)
	{
		var entity = await _definitionRepository.GetAsync(id);
		if (entity.TenantId != CurrentTenant.Id) throw new AbpAuthorizationException("Route definition belongs to another tenant.");
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(entity.AcademicYearId, nameof(GetDefinitionAsync)); return entity;
	}

	private void EnsureDefinitionCanBeAssigned(
		AdmissionCrmRouteDefinition definition,
		AdmissionInquiryLead lead)
	{
		var capturedDefinitionCanComplete = lead.IntendedRouteDefinitionId == definition.Id &&
		                                   lead.IntendedRouteDefinitionVersion == definition.Version &&
		                                   definition.Status is AdmissionCrmRouteDefinitionStatus.Published
			                                   or AdmissionCrmRouteDefinitionStatus.Retired;
		if (definition.AcademicYearId != lead.AcademicYearId ||
		    (!definition.IsEffectiveAt(Clock.Now) && !capturedDefinitionCanComplete))
		{
			throw new BusinessException("AdmissionCrm:PublishedRouteDefinitionRequired")
				.WithData("RouteDefinitionId", definition.Id)
				.WithData("AcademicYearId", lead.AcademicYearId);
		}
	}

	private async Task<AdmissionInquiryLead> GetLeadAsync(Guid id, string operation)
	{
		var lead = await _leadRepository.GetAsync(id); await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(lead.AcademicYearId, operation); return lead;
	}

	private async Task<AdmissionInquiryLead> ResolveImportLeadAsync(
		Guid academicYearId,
		ImportAdmissionCrmRteAllotmentRowDto row,
		string operation)
	{
		if (row.LeadId.HasValue && row.LeadId.Value != Guid.Empty)
		{
			return await GetLeadAsync(row.LeadId.Value, operation);
		}

		var reference = row.ApplicantReference?.Trim();
		if (string.IsNullOrWhiteSpace(reference))
		{
			throw new BusinessException("AdmissionCrm:RteApplicantReferenceRequired");
		}

		var normalizedContact = new string(reference.Where(char.IsDigit).ToArray());
		var normalizedEmail = reference.ToLowerInvariant();
		var queryable = await _leadRepository.GetQueryableAsync();
		var candidates = await AsyncExecuter.ToListAsync(queryable.Where(lead =>
			lead.TenantId == CurrentTenant.Id &&
			lead.AcademicYearId == academicYearId &&
			(lead.RegistrationFormNumber == reference ||
			 lead.NormalizedEmail == normalizedEmail ||
			 (normalizedContact.Length >= 7 && lead.NormalizedPrimaryContactNo == normalizedContact))));
		if (candidates.Count == 0)
		{
			throw new BusinessException("AdmissionCrm:RteApplicantReferenceNotFound");
		}
		if (candidates.Count > 1)
		{
			throw new BusinessException("AdmissionCrm:RteApplicantReferenceAmbiguous");
		}

		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(candidates[0].AcademicYearId, operation);
		return candidates[0];
	}

	private static AdmissionCrmRouteDefinitionDto MapDefinition(AdmissionCrmRouteDefinition x) => new()
	{
		Id = x.Id, AcademicYearId = x.AcademicYearId, Code = x.Code, Name = x.Name, RouteType = x.RouteType,
		Version = x.Version, Status = x.Status, Configuration = Deserialize<AdmissionCrmRouteConfigurationDto>(x.ConfigurationJson) ?? new(),
		EffectiveFrom = x.EffectiveFrom, EffectiveUntil = x.EffectiveUntil
	};

	private static AdmissionCrmRouteAssignmentDto MapAssignment(AdmissionCrmRouteAssignment x) => new()
	{
		Id = x.Id, LeadId = x.LeadId, RouteDefinitionId = x.RouteDefinitionId, RouteDefinitionVersion = x.RouteDefinitionVersion,
		RouteType = x.RouteType, Status = x.Status, RouteData = Deserialize<AdmissionCrmSpecialRouteDataDto>(x.RouteDataJson) ?? new(),
		EvidenceReferences = Deserialize<IReadOnlyList<string>>(x.EvidenceJson) ?? [], PreviousAssignmentId = x.PreviousAssignmentId,
		AssignmentReason = x.AssignmentReason, IsPrimary = x.IsPrimary, AssignedAt = x.AssignedAt,
		EvaluatedAt = x.EvaluatedAt, EvaluationReason = x.EvaluationReason
	};

	private static AdmissionCrmRouteAppealDto MapAppeal(AdmissionCrmRouteAppeal x) => new()
	{
		Id = x.Id, LeadId = x.LeadId, RouteAssignmentId = x.RouteAssignmentId,
		OriginalStatus = x.OriginalStatus, RequestedStatus = x.RequestedStatus,
		Reason = x.Reason, EvidenceReferences = Deserialize<IReadOnlyList<string>>(x.EvidenceJson) ?? [],
		Status = x.Status, RequestedAt = x.RequestedAt, DecidedAt = x.DecidedAt,
		DecisionReason = x.DecisionReason
	};

	private async Task AddTimelineAsync(AdmissionInquiryLead lead, AdmissionCrmTimelineEventType type, string title, string detail, Guid relatedId) =>
		await _timelineRepository.InsertAsync(new AdmissionCrmTimelineEvent(GuidGenerator.Create(), CurrentTenant.Id, lead.Id,
			lead.AcademicYearId, type, title, detail, relatedId, CurrentUser.Id, Clock.Now), autoSave: true);
	private Guid GetCurrentUserId() => CurrentUser.Id ?? throw new AbpAuthorizationException("An authenticated user is required.");
	private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
	private static T? Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value, JsonOptions);
}

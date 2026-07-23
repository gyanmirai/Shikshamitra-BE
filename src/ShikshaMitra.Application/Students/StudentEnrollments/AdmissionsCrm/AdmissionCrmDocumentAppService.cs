using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmDocumentAppService : ApplicationService, IAdmissionCrmDocumentAppService
{
	private const int MaxRejectionReasonCount = 8;
	private const int MaxRejectionReasonLength = 160;

	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private readonly IRepository<AdmissionCrmDocumentChecklistTemplate, Guid> _templateRepository;
	private readonly IRepository<AdmissionCrmDocumentChecklistAssignment, Guid> _assignmentRepository;
	private readonly IRepository<AdmissionCrmApplicantDocument, Guid> _documentRepository;
	private readonly IRepository<AdmissionCrmRegistrationApplication, Guid> _applicationRepository;
	private readonly IRepository<AdmissionInquiryLead, Guid> _leadRepository;
	private readonly IRepository<AdmissionCrmDocumentType, Guid> _documentTypeRepository;
	private readonly IRepository<AdmissionCrmTimelineEvent, Guid> _timelineRepository;
	private readonly StudentScopeAccessService _studentScopeAccessService;
	private readonly IBlobContainer _blobContainer;

	public AdmissionCrmDocumentAppService(
		IRepository<AdmissionCrmDocumentChecklistTemplate, Guid> templateRepository,
		IRepository<AdmissionCrmDocumentChecklistAssignment, Guid> assignmentRepository,
		IRepository<AdmissionCrmApplicantDocument, Guid> documentRepository,
		IRepository<AdmissionCrmRegistrationApplication, Guid> applicationRepository,
		IRepository<AdmissionInquiryLead, Guid> leadRepository,
		IRepository<AdmissionCrmDocumentType, Guid> documentTypeRepository,
		IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository,
		StudentScopeAccessService studentScopeAccessService,
		IBlobContainer blobContainer)
	{
		_templateRepository = templateRepository;
		_assignmentRepository = assignmentRepository;
		_documentRepository = documentRepository;
		_applicationRepository = applicationRepository;
		_leadRepository = leadRepository;
		_documentTypeRepository = documentTypeRepository;
		_timelineRepository = timelineRepository;
		_studentScopeAccessService = studentScopeAccessService;
		_blobContainer = blobContainer;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<PagedResultDto<AdmissionCrmDocumentChecklistTemplateDto>> GetTemplatesAsync(
		Guid academicYearId,
		GetAdmissionCrmDocumentChecklistTemplateListInput input)
	{
		await EnsureYearAccessAsync(academicYearId, nameof(GetTemplatesAsync));
		var queryable = await _templateRepository.GetQueryableAsync();
		var items = await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId &&
			(!input.Status.HasValue || x.Status == input.Status.Value) &&
			(string.IsNullOrWhiteSpace(input.Filter) || x.Name.Contains(input.Filter))));
		var ordered = items.OrderBy(x => x.Name).ThenByDescending(x => x.Version).ToList();
		return new PagedResultDto<AdmissionCrmDocumentChecklistTemplateDto>(
			ordered.Count,
			ordered.Skip(input.SkipCount).Take(input.MaxResultCount).Select(ToTemplateDto).ToList());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageDocumentConfiguration)]
	public async Task<AdmissionCrmDocumentChecklistTemplateDto> CreateTemplateAsync(
		Guid academicYearId,
		AdmissionCrmDocumentChecklistTemplateUpsertDto input)
	{
		await EnsureYearAccessAsync(academicYearId, nameof(CreateTemplateAsync));
		await NormalizeAndValidateTemplateAsync(input);
		var queryable = await _templateRepository.GetQueryableAsync();
		var versions = await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId && x.Name == input.Name));
		var template = CreateTemplateEntity(academicYearId, versions.Count == 0 ? 1 : versions.Max(x => x.Version) + 1, input);
		await _templateRepository.InsertAsync(template, autoSave: true);
		return ToTemplateDto(template);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageDocumentConfiguration)]
	public async Task<AdmissionCrmDocumentChecklistTemplateDto> UpdateTemplateAsync(
		Guid id,
		AdmissionCrmDocumentChecklistTemplateUpsertDto input)
	{
		var template = await GetTemplateAsync(id, nameof(UpdateTemplateAsync));
		await NormalizeAndValidateTemplateAsync(input);
		template.Update(input.Name, input.IsFallback, Serialize(input.Applicability), Serialize(input.Requirements), input.ReadinessApprovalCount, input.Notes);
		await _templateRepository.UpdateAsync(template, autoSave: true);
		return ToTemplateDto(template);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageDocumentConfiguration)]
	public async Task<AdmissionCrmDocumentChecklistTemplateDto> PublishTemplateAsync(Guid id)
	{
		var template = await GetTemplateAsync(id, nameof(PublishTemplateAsync));
		await NormalizeAndValidateTemplateAsync(ToTemplateUpsert(template));
		await EnsureNoPublishedConflictAsync(template);
		template.Publish(Clock.Now);
		await _templateRepository.UpdateAsync(template, autoSave: true);
		return ToTemplateDto(template);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageDocumentConfiguration)]
	public async Task<AdmissionCrmDocumentChecklistTemplateDto> CreateTemplateVersionAsync(Guid id)
	{
		var source = await GetTemplateAsync(id, nameof(CreateTemplateVersionAsync));
		var queryable = await _templateRepository.GetQueryableAsync();
		var versions = await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == source.AcademicYearId && x.Name == source.Name));
		var draft = CreateTemplateEntity(source.AcademicYearId, versions.Max(x => x.Version) + 1, ToTemplateUpsert(source));
		await _templateRepository.InsertAsync(draft, autoSave: true);
		return ToTemplateDto(draft);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageDocumentConfiguration)]
	public async Task<AdmissionCrmDocumentChecklistTemplateDto> RetireTemplateAsync(Guid id)
	{
		var template = await GetTemplateAsync(id, nameof(RetireTemplateAsync));
		template.Retire(Clock.Now);
		await _templateRepository.UpdateAsync(template, autoSave: true);
		return ToTemplateDto(template);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageDocumentConfiguration)]
	public async Task<AdmissionCrmApplicantDocumentChecklistDto> AssignChecklistAsync(
		Guid registrationApplicationId,
		AssignAdmissionCrmDocumentChecklistDto input)
	{
		var application = await GetApplicationAsync(registrationApplicationId, nameof(AssignChecklistAsync));
		var template = await GetTemplateAsync(input.ChecklistTemplateId, nameof(AssignChecklistAsync));
		if (template.AcademicYearId != application.AcademicYearId || template.Status != AdmissionCrmDocumentChecklistTemplateStatus.Published)
			throw new BusinessException("AdmissionCrm:PublishedDocumentChecklistRequired");
		var assignmentQueryable = await _assignmentRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(assignmentQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.RegistrationApplicationId == registrationApplicationId)))
			throw new BusinessException("AdmissionCrm:DocumentChecklistAlreadyAssigned");
		var assignment = new AdmissionCrmDocumentChecklistAssignment(
			GuidGenerator.Create(), CurrentTenant.Id, application.AcademicYearId, application.LeadId, application.Id,
			template.Id, template.Version, Serialize(ToTemplateDto(template)), Clock.Now);
		await _assignmentRepository.InsertAsync(assignment, autoSave: true);
		await AddTimelineAsync(application.LeadId, application.AcademicYearId, AdmissionCrmTimelineEventType.DocumentChecklistAssigned,
			"Document checklist assigned", $"{template.Name} v{template.Version}.", assignment.Id);
		return await BuildChecklistDtoAsync(assignment, application);
	}

	public async Task<AdmissionCrmApplicantDocumentChecklistDto?> AssignApplicableChecklistFromAutomationAsync(
		Guid registrationApplicationId,
		AdmissionCrmPlaybookApplicabilityDto applicability)
	{
		var application = await GetApplicationAsync(registrationApplicationId, nameof(AssignApplicableChecklistFromAutomationAsync));
		var assignmentQueryable = await _assignmentRepository.GetQueryableAsync();
		var existing = await AsyncExecuter.FirstOrDefaultAsync(assignmentQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.RegistrationApplicationId == registrationApplicationId));
		if (existing != null) return await BuildChecklistDtoAsync(existing, application);

		var templateQueryable = await _templateRepository.GetQueryableAsync();
		var published = await AsyncExecuter.ToListAsync(templateQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == application.AcademicYearId &&
			x.Status == AdmissionCrmDocumentChecklistTemplateStatus.Published));
		var selected = published
			.Select(template => new
			{
				Template = template,
				Scope = Deserialize<AdmissionCrmPlaybookApplicabilityDto>(template.ApplicabilityJson)
			})
			.Where(x => MatchesApplicability(x.Scope, applicability))
			.OrderByDescending(x => ApplicabilityScore(x.Scope))
			.ThenByDescending(x => x.Template.Version)
			.Select(x => x.Template)
			.FirstOrDefault();
		if (selected == null) return null;
		var assignment = new AdmissionCrmDocumentChecklistAssignment(
			GuidGenerator.Create(), CurrentTenant.Id, application.AcademicYearId, application.LeadId, application.Id,
			selected.Id, selected.Version, Serialize(ToTemplateDto(selected)), Clock.Now);
		await _assignmentRepository.InsertAsync(assignment, autoSave: true);
		await AddTimelineAsync(application.LeadId, application.AcademicYearId, AdmissionCrmTimelineEventType.DocumentChecklistAssigned,
			"Document checklist assigned automatically", $"{selected.Name} v{selected.Version}.", assignment.Id);
		return await BuildChecklistDtoAsync(assignment, application);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<AdmissionCrmApplicantDocumentChecklistDto> GetChecklistAsync(Guid registrationApplicationId)
	{
		var application = await GetApplicationAsync(registrationApplicationId, nameof(GetChecklistAsync));
		var assignment = await GetAssignmentByApplicationAsync(registrationApplicationId);
		return await BuildChecklistDtoAsync(assignment, application);
	}

	[AllowAnonymous]
	public async Task<AdmissionCrmApplicantDocumentChecklistDto?> GetChecklistFromApplicantAsync(Guid leadId)
	{
		var assignment = await GetAssignmentByLeadOrDefaultAsync(leadId);
		if (assignment == null) return null;
		var application = await _applicationRepository.GetAsync(assignment.RegistrationApplicationId);
		return await BuildChecklistDtoAsync(assignment, application);
	}

	[AllowAnonymous]
	public async Task<AdmissionCrmApplicantDocumentChecklistDto> UploadFromApplicantAsync(
		Guid leadId,
		ProcessedAdmissionCrmApplicantDocumentUploadDto input)
	{
		var assignment = await GetAssignmentByLeadOrDefaultAsync(leadId)
		                 ?? throw new BusinessException("AdmissionCrm:DocumentChecklistNotAssigned");
		var application = await _applicationRepository.GetAsync(assignment.RegistrationApplicationId);
		return await ProcessUploadAsync(assignment, application, input, actorUserId: null);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ReviewDocuments)]
	public async Task<AdmissionCrmApplicantDocumentChecklistDto> UploadFromStaffAsync(
		Guid registrationApplicationId,
		ProcessedAdmissionCrmApplicantDocumentUploadDto input)
	{
		var application = await GetApplicationAsync(registrationApplicationId, nameof(UploadFromStaffAsync));
		var assignment = await GetAssignmentByApplicationAsync(registrationApplicationId);
		return await ProcessUploadAsync(assignment, application, input, CurrentUser.Id);
	}

	private async Task<AdmissionCrmApplicantDocumentChecklistDto> ProcessUploadAsync(
		AdmissionCrmDocumentChecklistAssignment assignment,
		AdmissionCrmRegistrationApplication application,
		ProcessedAdmissionCrmApplicantDocumentUploadDto input,
		Guid? actorUserId)
	{
		var leadId = application.LeadId;
		var template = Deserialize<AdmissionCrmDocumentChecklistTemplateDto>(assignment.TemplateSnapshotJson);
		var requirement = template.Requirements.FirstOrDefault(x =>
			string.Equals(x.Key, input.RequirementKey, StringComparison.OrdinalIgnoreCase))
			?? throw new BusinessException("AdmissionCrm:DocumentRequirementNotFound");
		ValidateUpload(requirement, input);

		var current = await GetCurrentDocumentOrDefaultAsync(assignment.Id, requirement.Key);
		var hash = Convert.ToHexString(SHA256.HashData(input.Content)).ToLowerInvariant();
		if (current?.Sha256Hash == hash)
			throw new BusinessException("AdmissionCrm:DuplicateDocumentUpload");
		var safeName = SanitizeFileName(input.FileName);
		var blobName = $"admissions-crm/{CurrentTenant.Id:D}/{leadId:D}/{requirement.Key}/{Guid.NewGuid():N}/{safeName}";
		await _blobContainer.SaveAsync(blobName, input.Content, overrideExisting: false);
		try
		{
			var document = new AdmissionCrmApplicantDocument(
				GuidGenerator.Create(), CurrentTenant.Id, assignment.AcademicYearId, leadId, assignment.Id, requirement.Key,
				(current?.Version ?? 0) + 1, blobName, safeName, input.ContentType, input.Size, hash, input.ValidUntil, Clock.Now);
			current?.MarkSuperseded(Clock.Now);
			if (current != null) await _documentRepository.UpdateAsync(current, autoSave: false);
			await _documentRepository.InsertAsync(document, autoSave: true);
			await AddTimelineAsync(leadId, assignment.AcademicYearId, AdmissionCrmTimelineEventType.DocumentUploaded,
				"Document uploaded", $"{requirement.Name} v{document.Version}.", document.Id, actorUserId);
		}
		catch
		{
			await _blobContainer.DeleteAsync(blobName);
			throw;
		}
		return await BuildChecklistDtoAsync(assignment, application);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ReviewDocuments)]
	public async Task<AdmissionCrmApplicantDocumentDto> ReviewAsync(
		Guid documentId,
		ReviewAdmissionCrmApplicantDocumentDto input)
	{
		var document = await GetDocumentAsync(documentId, nameof(ReviewAsync));
		var assignment = await _assignmentRepository.GetAsync(document.ChecklistAssignmentId);
		var requirement = GetRequirement(assignment, document.RequirementKey);
		if (input.Approve) document.Verify(CurrentUser.Id!.Value, Clock.Now);
		else document.Reject(CurrentUser.Id!.Value, Check.NotNullOrWhiteSpace(input.Reason, nameof(input.Reason), AdmissionCrmConsts.MaxNotesLength), Clock.Now);
		await _documentRepository.UpdateAsync(document, autoSave: true);
		await AddTimelineAsync(document.LeadId, document.AcademicYearId,
			input.Approve ? AdmissionCrmTimelineEventType.DocumentVerified : AdmissionCrmTimelineEventType.DocumentRejected,
			input.Approve ? "Document verified" : "Document rejected",
			input.Approve ? requirement.Name : input.Reason, document.Id);
		return ToDocumentDto(requirement, document, true);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.WaiveDocuments)]
	public async Task<AdmissionCrmApplicantDocumentDto> WaiveAsync(
		Guid assignmentId,
		string requirementKey,
		AdmissionCrmDocumentReasonDto input)
	{
		var assignment = await GetAssignmentAsync(assignmentId, nameof(WaiveAsync));
		var requirement = GetRequirement(assignment, requirementKey);
		var current = await GetCurrentDocumentOrDefaultAsync(assignment.Id, requirement.Key);
		current?.MarkSuperseded(Clock.Now);
		if (current != null) await _documentRepository.UpdateAsync(current, autoSave: false);
		var waiver = AdmissionCrmApplicantDocument.CreateWaiver(
			GuidGenerator.Create(), CurrentTenant.Id, assignment.AcademicYearId, assignment.LeadId, assignment.Id,
			requirement.Key, (current?.Version ?? 0) + 1, CurrentUser.Id!.Value, input.Reason, Clock.Now);
		await _documentRepository.InsertAsync(waiver, autoSave: true);
		await AddTimelineAsync(assignment.LeadId, assignment.AcademicYearId, AdmissionCrmTimelineEventType.DocumentWaived,
			"Document requirement waived", input.Reason, waiver.Id);
		return ToDocumentDto(requirement, waiver, true);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ConfirmOriginalSeen)]
	public async Task<AdmissionCrmApplicantDocumentDto> MarkOriginalSeenAsync(Guid documentId)
	{
		var document = await GetDocumentAsync(documentId, nameof(MarkOriginalSeenAsync));
		var assignment = await _assignmentRepository.GetAsync(document.ChecklistAssignmentId);
		var requirement = GetRequirement(assignment, document.RequirementKey);
		document.MarkOriginalSeen(CurrentUser.Id!.Value, Clock.Now);
		await _documentRepository.UpdateAsync(document, autoSave: true);
		await AddTimelineAsync(document.LeadId, document.AcademicYearId, AdmissionCrmTimelineEventType.DocumentOriginalSeen,
			"Original document seen", requirement.Name, document.Id);
		return ToDocumentDto(requirement, document, true);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ConfirmAdmissionReadiness)]
	public async Task<AdmissionCrmApplicantDocumentChecklistDto> ConfirmReadinessAsync(
		Guid assignmentId,
		ConfirmAdmissionCrmReadinessDto input)
	{
		var assignment = await GetAssignmentAsync(assignmentId, nameof(ConfirmReadinessAsync));
		var application = await GetApplicationAsync(assignment.RegistrationApplicationId, nameof(ConfirmReadinessAsync));
		var checklist = await BuildChecklistDtoAsync(assignment, application);
		if (!checklist.AdmissionReady && !input.OverrideBlockingRequirements)
			throw new BusinessException("AdmissionCrm:AdmissionReadinessBlocked").WithData("BlockingReasons", checklist.BlockingReasons);
		if (input.OverrideBlockingRequirements)
		{
			if (!await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.OverrideAdmissionReadiness))
				throw new Volo.Abp.Authorization.AbpAuthorizationException();
			Check.NotNullOrWhiteSpace(input.Reason, nameof(input.Reason), AdmissionCrmConsts.MaxNotesLength);
		}
		var completed = assignment.ConfirmReadiness(
			CurrentUser.Id!.Value,
			checklist.RequiredReadinessApprovalCount,
			input.OverrideBlockingRequirements,
			input.Reason,
			Clock.Now);
		if (completed)
			application.MarkAdmissionReady();
		await _assignmentRepository.UpdateAsync(assignment, autoSave: false);
		if (completed)
			await _applicationRepository.UpdateAsync(application, autoSave: false);
		await AddTimelineAsync(assignment.LeadId, assignment.AcademicYearId,
			completed
				? assignment.ReadinessOverridden ? AdmissionCrmTimelineEventType.AdmissionReadinessOverridden : AdmissionCrmTimelineEventType.AdmissionReadinessConfirmed
				: AdmissionCrmTimelineEventType.AdmissionReadinessApprovalRecorded,
			completed
				? assignment.ReadinessOverridden ? "Admission readiness overridden" : "Admission readiness confirmed"
				: "First admission readiness approval recorded",
			input.Reason, assignment.Id);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await BuildChecklistDtoAsync(assignment, application);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ReviewDocuments)]
	public async Task<AdmissionCrmApplicantDocumentDownloadDto> DownloadAsync(Guid documentId)
	{
		var document = await GetDocumentAsync(documentId, nameof(DownloadAsync));
		if (string.IsNullOrWhiteSpace(document.BlobName)) throw new BusinessException("AdmissionCrm:DocumentFileNotAvailable");
		return new AdmissionCrmApplicantDocumentDownloadDto
		{
			FileName = document.FileName!, ContentType = document.ContentType!, Content = await _blobContainer.GetAllBytesAsync(document.BlobName)
		};
	}

	private async Task NormalizeAndValidateTemplateAsync(AdmissionCrmDocumentChecklistTemplateUpsertDto input)
	{
		input.Name = Check.NotNullOrWhiteSpace(input.Name, nameof(input.Name), AdmissionCrmConsts.MaxTemplateNameLength).Trim();
		if (input.IsFallback && HasApplicability(input.Applicability))
			throw new BusinessException("AdmissionCrm:FallbackDocumentChecklistCannotHaveApplicability");
		if (input.Requirements.Count == 0) throw new BusinessException("AdmissionCrm:DocumentChecklistRequiresItem");
		var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var requirement in input.Requirements)
		{
			requirement.Key = Check.NotNullOrWhiteSpace(requirement.Key, nameof(requirement.Key), 80).Trim();
			requirement.Name = Check.NotNullOrWhiteSpace(requirement.Name, nameof(requirement.Name), 120).Trim();
			if (!keys.Add(requirement.Key)) throw new BusinessException("AdmissionCrm:DuplicateDocumentRequirementKey");
			if (requirement.RequirementLevel == AdmissionCrmDocumentRequirementLevel.Conditional && string.IsNullOrWhiteSpace(requirement.ConditionalFieldKey))
				throw new BusinessException("AdmissionCrm:ConditionalDocumentRequirementNeedsField");
			if (requirement.ExpiryReminderDaysBefore.HasValue && !requirement.ValidityDateRequired)
				throw new BusinessException("AdmissionCrm:DocumentExpiryReminderRequiresValidityDate")
					.WithData("RequirementKey", requirement.Key);
			var extensions = requirement.AcceptedExtensions.Select(x => x.Trim().TrimStart('.').ToLowerInvariant()).Where(x => x.Length > 0).Distinct().ToList();
			if (extensions.Count == 0 || extensions.Any(x => x.Any(character => !char.IsLetterOrDigit(character))))
				throw new BusinessException("AdmissionCrm:InvalidDocumentExtensions");
			requirement.AcceptedExtensions = extensions;
			requirement.UploadInstructions = string.IsNullOrWhiteSpace(requirement.UploadInstructions)
				? null
				: requirement.UploadInstructions.Trim();
			var rejectionReasons = (requirement.RejectionReasons ?? [])
				.Select(x => x.Trim())
				.Where(x => x.Length > 0)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();
			if (rejectionReasons.Count > MaxRejectionReasonCount ||
			    rejectionReasons.Any(x => x.Length > MaxRejectionReasonLength))
				throw new BusinessException("AdmissionCrm:InvalidDocumentRejectionReasons")
					.WithData("RequirementKey", requirement.Key)
					.WithData("MaximumCount", MaxRejectionReasonCount)
					.WithData("MaximumLength", MaxRejectionReasonLength);
			requirement.RejectionReasons = rejectionReasons;
			if (requirement.DocumentTypeId.HasValue)
			{
				var type = await _documentTypeRepository.GetAsync(requirement.DocumentTypeId.Value);
				if (type.TenantId != CurrentTenant.Id) throw new BusinessException("AdmissionCrm:DocumentTypeNotFound");
			}
		}
	}

	private static void ValidateUpload(AdmissionCrmDocumentRequirementDto requirement, ProcessedAdmissionCrmApplicantDocumentUploadDto input)
	{
		if (input.Content.Length == 0 || input.Size != input.Content.LongLength) throw new BusinessException("AdmissionCrm:InvalidDocumentContent");
		var maxBytes = (long)(requirement.MaximumSizeMb * 1024 * 1024);
		if (input.Size > maxBytes) throw new BusinessException("AdmissionCrm:DocumentFileSizeExceeded").WithData("MaximumSizeMb", requirement.MaximumSizeMb);
		var extension = Path.GetExtension(input.FileName).TrimStart('.').ToLowerInvariant();
		if (!requirement.AcceptedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
			throw new BusinessException("AdmissionCrm:DocumentFileTypeNotAccepted").WithData("Extension", extension);
		if (requirement.ValidityDateRequired && !input.ValidUntil.HasValue)
			throw new BusinessException("AdmissionCrm:DocumentValidityDateRequired");
		if (input.ValidUntil.HasValue && input.ValidUntil.Value.Date < DateTime.UtcNow.Date)
			throw new BusinessException("AdmissionCrm:DocumentAlreadyExpired");
	}

	private async Task<AdmissionCrmApplicantDocumentChecklistDto> BuildChecklistDtoAsync(
		AdmissionCrmDocumentChecklistAssignment assignment,
		AdmissionCrmRegistrationApplication application)
	{
		var template = Deserialize<AdmissionCrmDocumentChecklistTemplateDto>(assignment.TemplateSnapshotJson);
		var queryable = await _documentRepository.GetQueryableAsync();
		var allDocuments = await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.ChecklistAssignmentId == assignment.Id));
		var currentByRequirement = allDocuments.Where(x => !x.SupersededAt.HasValue)
			.GroupBy(x => x.RequirementKey, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.Version).First(), StringComparer.OrdinalIgnoreCase);
		var responses = Deserialize<Dictionary<string, JsonElement>>(application.ResponseJson);
		var prefill = Deserialize<Dictionary<string, JsonElement>>(application.PrefillJson);
		var documents = template.Requirements.OrderBy(x => x.DisplayOrder).Select(requirement =>
		{
			var current = currentByRequirement.GetValueOrDefault(requirement.Key);
			var required = IsRequirementCurrentlyRequired(requirement, responses, prefill);
			return ToDocumentDto(requirement, current, required);
		}).ToList();
		var blockingReasons = documents.Where(x => x.IsCurrentlyRequired && x.BlocksAdmissionReadiness && !IsDocumentComplete(x))
			.Select(x => $"{x.RequirementName}: {GetIncompleteReason(x)}").ToList();
		var requiredDocuments = documents.Where(x => x.IsCurrentlyRequired).ToList();
		var completed = requiredDocuments.Count(IsDocumentComplete);
		return new AdmissionCrmApplicantDocumentChecklistDto
		{
			AssignmentId = assignment.Id,
			RegistrationApplicationId = assignment.RegistrationApplicationId,
			ChecklistTemplateId = assignment.ChecklistTemplateId,
			ChecklistTemplateVersion = assignment.ChecklistTemplateVersion,
			ChecklistName = template.Name,
			CompletionPercentage = requiredDocuments.Count == 0 ? 100 : Math.Round((decimal)completed * 100 / requiredDocuments.Count, 2),
			AdmissionReady = blockingReasons.Count == 0 && application.Status == AdmissionCrmRegistrationStatus.Verified,
			ReadinessConfirmed = assignment.ReadinessConfirmedAt.HasValue,
			ReadinessOverridden = assignment.ReadinessOverridden,
			RequiredReadinessApprovalCount = template.ReadinessApprovalCount,
			ReceivedReadinessApprovalCount = assignment.ReadinessApprovalCount,
			AwaitingSecondReadinessApproval = template.ReadinessApprovalCount == 2 && assignment.ReadinessApprovalCount == 1,
			BlockingReasons = blockingReasons,
			Documents = documents
		};
	}

	private static AdmissionCrmApplicantDocumentDto ToDocumentDto(
		AdmissionCrmDocumentRequirementDto requirement,
		AdmissionCrmApplicantDocument? document,
		bool required)
	{
		return new AdmissionCrmApplicantDocumentDto
		{
			Id = document?.Id, RequirementKey = requirement.Key, RequirementName = requirement.Name,
			RequirementLevel = requirement.RequirementLevel, IsCurrentlyRequired = required,
			BlocksAdmissionReadiness = requirement.BlocksAdmissionReadiness, RequiresOriginal = requirement.RequiresOriginal,
			ValidityDateRequired = requirement.ValidityDateRequired,
			ExpiryReminderDaysBefore = requirement.ExpiryReminderDaysBefore,
			OriginalSeen = document?.OriginalSeenAt.HasValue == true || document?.Status == AdmissionCrmApplicantDocumentStatus.OriginalSeen,
			Status = document?.ValidUntil < DateTime.UtcNow ? AdmissionCrmApplicantDocumentStatus.Expired : document?.Status,
			Version = document?.Version ?? 0, FileName = document?.FileName, ContentType = document?.ContentType,
			FileSize = document?.FileSize, ValidUntil = document?.ValidUntil, UploadedAt = document?.UploadedAt,
			DecisionReason = document?.DecisionReason, DecisionAt = document?.DecisionAt,
			AcceptedExtensions = requirement.AcceptedExtensions, MaximumSizeMb = requirement.MaximumSizeMb,
			UploadInstructions = requirement.UploadInstructions,
			RejectionReasons = requirement.RejectionReasons
		};
	}

	private static bool IsDocumentComplete(AdmissionCrmApplicantDocumentDto document)
	{
		if (document.Status == AdmissionCrmApplicantDocumentStatus.Waived) return true;
		if (document.RequiresOriginal) return document.Status == AdmissionCrmApplicantDocumentStatus.OriginalSeen;
		return document.Status is AdmissionCrmApplicantDocumentStatus.Verified or AdmissionCrmApplicantDocumentStatus.OriginalSeen;
	}

	private static string GetIncompleteReason(AdmissionCrmApplicantDocumentDto document) => document.Status switch
	{
		null => "missing",
		AdmissionCrmApplicantDocumentStatus.Rejected => "rejected",
		AdmissionCrmApplicantDocumentStatus.Expired => "expired",
		AdmissionCrmApplicantDocumentStatus.Uploaded or AdmissionCrmApplicantDocumentStatus.UnderReview => "awaiting verification",
		AdmissionCrmApplicantDocumentStatus.Verified when document.RequiresOriginal => "original not seen",
		_ => "unresolved"
	};

	private static bool IsRequirementCurrentlyRequired(
		AdmissionCrmDocumentRequirementDto requirement,
		IReadOnlyDictionary<string, JsonElement> responses,
		IReadOnlyDictionary<string, JsonElement> prefill)
	{
		if (requirement.RequirementLevel is AdmissionCrmDocumentRequirementLevel.Required or AdmissionCrmDocumentRequirementLevel.RequiredLater) return true;
		if (requirement.RequirementLevel == AdmissionCrmDocumentRequirementLevel.Optional) return false;
		if (string.IsNullOrWhiteSpace(requirement.ConditionalFieldKey)) return false;
		if (responses.TryGetValue(requirement.ConditionalFieldKey, out var response))
			return string.Equals(JsonText(response), requirement.ConditionalFieldValue, StringComparison.OrdinalIgnoreCase);
		if (prefill.TryGetValue(requirement.ConditionalFieldKey, out var value))
			return string.Equals(JsonText(value), requirement.ConditionalFieldValue, StringComparison.OrdinalIgnoreCase);
		return false;
	}

	private static string JsonText(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();

	private async Task<AdmissionCrmApplicantDocument?> GetCurrentDocumentOrDefaultAsync(Guid assignmentId, string requirementKey)
	{
		var queryable = await _documentRepository.GetQueryableAsync();
		var documents = await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.ChecklistAssignmentId == assignmentId && x.RequirementKey == requirementKey));
		return documents.Where(x => !x.SupersededAt.HasValue).OrderByDescending(x => x.Version).FirstOrDefault();
	}

	private async Task<AdmissionCrmDocumentChecklistAssignment?> GetAssignmentByLeadOrDefaultAsync(Guid leadId)
	{
		var queryable = await _assignmentRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.LeadId == leadId));
	}

	private async Task<AdmissionCrmDocumentChecklistAssignment> GetAssignmentByApplicationAsync(Guid applicationId)
	{
		var queryable = await _assignmentRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.RegistrationApplicationId == applicationId))
		       ?? throw new BusinessException("AdmissionCrm:DocumentChecklistNotAssigned");
	}

	private async Task<AdmissionCrmDocumentChecklistAssignment> GetAssignmentAsync(Guid id, string operation)
	{
		var assignment = await _assignmentRepository.GetAsync(id);
		if (assignment.TenantId != CurrentTenant.Id) throw new BusinessException("AdmissionCrm:DocumentChecklistAssignmentNotFound");
		await EnsureYearAccessAsync(assignment.AcademicYearId, operation);
		return assignment;
	}

	private async Task<AdmissionCrmRegistrationApplication> GetApplicationAsync(Guid id, string operation)
	{
		var application = await _applicationRepository.GetAsync(id);
		if (application.TenantId != CurrentTenant.Id) throw new BusinessException("AdmissionCrm:RegistrationApplicationNotFound");
		await EnsureYearAccessAsync(application.AcademicYearId, operation);
		return application;
	}

	private async Task<AdmissionCrmDocumentChecklistTemplate> GetTemplateAsync(Guid id, string operation)
	{
		var template = await _templateRepository.GetAsync(id);
		if (template.TenantId != CurrentTenant.Id) throw new BusinessException("AdmissionCrm:DocumentChecklistTemplateNotFound");
		await EnsureYearAccessAsync(template.AcademicYearId, operation);
		return template;
	}

	private async Task<AdmissionCrmApplicantDocument> GetDocumentAsync(Guid id, string operation)
	{
		var document = await _documentRepository.GetAsync(id);
		if (document.TenantId != CurrentTenant.Id || document.SupersededAt.HasValue) throw new BusinessException("AdmissionCrm:ApplicantDocumentNotFound");
		await EnsureYearAccessAsync(document.AcademicYearId, operation);
		return document;
	}

	private async Task EnsureYearAccessAsync(Guid academicYearId, string operation) =>
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(academicYearId, operation);

	private AdmissionCrmDocumentChecklistTemplate CreateTemplateEntity(Guid yearId, int version, AdmissionCrmDocumentChecklistTemplateUpsertDto input) =>
		new(GuidGenerator.Create(), CurrentTenant.Id, yearId, input.Name, version, input.IsFallback,
			Serialize(input.Applicability), Serialize(input.Requirements), input.ReadinessApprovalCount, input.Notes);

	private static AdmissionCrmDocumentChecklistTemplateUpsertDto ToTemplateUpsert(AdmissionCrmDocumentChecklistTemplate value) => new()
	{
		Name = value.Name, IsFallback = value.IsFallback,
		Applicability = Deserialize<AdmissionCrmPlaybookApplicabilityDto>(value.ApplicabilityJson),
		Requirements = Deserialize<List<AdmissionCrmDocumentRequirementDto>>(value.RequirementsJson),
		ReadinessApprovalCount = value.ReadinessApprovalCount,
		Notes = value.Notes
	};

	private static AdmissionCrmDocumentChecklistTemplateDto ToTemplateDto(AdmissionCrmDocumentChecklistTemplate value)
	{
		var input = ToTemplateUpsert(value);
		return new AdmissionCrmDocumentChecklistTemplateDto
		{
			Id = value.Id, AcademicYearId = value.AcademicYearId, Version = value.Version, Status = value.Status,
			PublishedAt = value.PublishedAt, RetiredAt = value.RetiredAt, Name = input.Name, IsFallback = input.IsFallback,
			Applicability = input.Applicability, Requirements = input.Requirements,
			ReadinessApprovalCount = input.ReadinessApprovalCount, Notes = input.Notes
		};
	}

	private static AdmissionCrmDocumentRequirementDto GetRequirement(AdmissionCrmDocumentChecklistAssignment assignment, string key) =>
		Deserialize<AdmissionCrmDocumentChecklistTemplateDto>(assignment.TemplateSnapshotJson).Requirements
			.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase))
		?? throw new BusinessException("AdmissionCrm:DocumentRequirementNotFound");

	private async Task EnsureNoPublishedConflictAsync(AdmissionCrmDocumentChecklistTemplate candidate)
	{
		var queryable = await _templateRepository.GetQueryableAsync();
		var published = await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == candidate.AcademicYearId &&
			x.Status == AdmissionCrmDocumentChecklistTemplateStatus.Published && x.Id != candidate.Id));
		if (published.Any(x => x.ApplicabilityJson == candidate.ApplicabilityJson))
			throw new BusinessException("AdmissionCrm:AmbiguousPublishedDocumentChecklist");
	}

	private async Task AddTimelineAsync(
		Guid leadId, Guid yearId, AdmissionCrmTimelineEventType type, string title, string? details,
		Guid relatedId, Guid? actorUserId = default)
	{
		await _timelineRepository.InsertAsync(new AdmissionCrmTimelineEvent(
			GuidGenerator.Create(), CurrentTenant.Id, leadId, yearId, type, title, details, relatedId,
			actorUserId == default ? CurrentUser.Id : actorUserId, new DateTimeOffset(Clock.Now)), autoSave: true);
	}

	private static bool HasApplicability(AdmissionCrmPlaybookApplicabilityDto value) =>
		new[] { value.Campus, value.DesiredClass, value.Board, value.Medium, value.ApplicantCategory, value.AdmissionRound, value.AdmissionRoute }
			.Any(x => !string.IsNullOrWhiteSpace(x));

	private static bool MatchesApplicability(
		AdmissionCrmPlaybookApplicabilityDto configured,
		AdmissionCrmPlaybookApplicabilityDto actual) =>
		Matches(configured.Campus, actual.Campus) && Matches(configured.DesiredClass, actual.DesiredClass) &&
		Matches(configured.Board, actual.Board) && Matches(configured.Medium, actual.Medium) &&
		Matches(configured.ApplicantCategory, actual.ApplicantCategory) && Matches(configured.AdmissionRound, actual.AdmissionRound) &&
		Matches(configured.AdmissionRoute, actual.AdmissionRoute);

	private static bool Matches(string? configured, string? actual) =>
		string.IsNullOrWhiteSpace(configured) || string.Equals(configured.Trim(), actual?.Trim(), StringComparison.OrdinalIgnoreCase);

	private static int ApplicabilityScore(AdmissionCrmPlaybookApplicabilityDto value) =>
		new[] { value.Campus, value.DesiredClass, value.Board, value.Medium, value.ApplicantCategory, value.AdmissionRound, value.AdmissionRoute }
			.Count(x => !string.IsNullOrWhiteSpace(x));

	private static string SanitizeFileName(string fileName)
	{
		var safe = Path.GetFileName(fileName);
		foreach (var character in Path.GetInvalidFileNameChars()) safe = safe.Replace(character, '_');
		return Check.NotNullOrWhiteSpace(safe, nameof(fileName), AdmissionCrmConsts.MaxDocumentFileNameLength);
	}

	private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
	private static T Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value, JsonOptions)
		?? throw new BusinessException("AdmissionCrm:InvalidStoredDocumentConfiguration");
}

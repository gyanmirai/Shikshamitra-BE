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
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Security.Encryption;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmRegistrationAppService : ApplicationService, IAdmissionCrmRegistrationAppService
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private static readonly StringComparer CodeComparer = StringComparer.OrdinalIgnoreCase;
	private static readonly HashSet<string> SupportedSystemFields = new(StringComparer.OrdinalIgnoreCase)
	{
		"firstName", "middleName", "lastName", "dateOfBirth", "parentGuardianName",
		"primaryContactNo", "email", "desiredClassName", "campus", "board", "medium",
		"admissionRound", "applicantCategory", "admissionRoute"
	};

	private readonly IRepository<AdmissionCrmRegistrationTemplate, Guid> _templateRepository;
	private readonly IRepository<AdmissionCrmFormNumberSeries, Guid> _seriesRepository;
	private readonly IRepository<AdmissionCrmRegistrationApplication, Guid> _applicationRepository;
	private readonly IRepository<AdmissionInquiryLead, Guid> _leadRepository;
	private readonly IRepository<AdmissionCrmCycleConfiguration, Guid> _cycleRepository;
	private readonly IRepository<AdmissionCrmPlaybook, Guid> _playbookRepository;
	private readonly IRepository<AdmissionCrmEntranceExamRecord, Guid> _examRepository;
	private readonly IRepository<AdmissionApplicantStatusAccess, Guid> _statusAccessRepository;
	private readonly IRepository<AdmissionCrmTimelineEvent, Guid> _timelineRepository;
	private readonly StudentScopeAccessService _studentScopeAccessService;
	private readonly StudentEnrollmentManager _studentEnrollmentManager;
	private readonly IStringEncryptionService _stringEncryptionService;
	private readonly AdmissionCrmDocumentChecklistWorkflow _documentChecklistWorkflow;

	public AdmissionCrmRegistrationAppService(
		IRepository<AdmissionCrmRegistrationTemplate, Guid> templateRepository,
		IRepository<AdmissionCrmFormNumberSeries, Guid> seriesRepository,
		IRepository<AdmissionCrmRegistrationApplication, Guid> applicationRepository,
		IRepository<AdmissionInquiryLead, Guid> leadRepository,
		IRepository<AdmissionCrmCycleConfiguration, Guid> cycleRepository,
		IRepository<AdmissionCrmPlaybook, Guid> playbookRepository,
		IRepository<AdmissionCrmEntranceExamRecord, Guid> examRepository,
		IRepository<AdmissionApplicantStatusAccess, Guid> statusAccessRepository,
		IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository,
		StudentScopeAccessService studentScopeAccessService,
		StudentEnrollmentManager studentEnrollmentManager,
		IStringEncryptionService stringEncryptionService,
		AdmissionCrmDocumentChecklistWorkflow documentChecklistWorkflow)
	{
		_templateRepository = templateRepository;
		_seriesRepository = seriesRepository;
		_applicationRepository = applicationRepository;
		_leadRepository = leadRepository;
		_cycleRepository = cycleRepository;
		_playbookRepository = playbookRepository;
		_examRepository = examRepository;
		_statusAccessRepository = statusAccessRepository;
		_timelineRepository = timelineRepository;
		_studentScopeAccessService = studentScopeAccessService;
		_studentEnrollmentManager = studentEnrollmentManager;
		_stringEncryptionService = stringEncryptionService;
		_documentChecklistWorkflow = documentChecklistWorkflow;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<PagedResultDto<AdmissionCrmRegistrationTemplateDto>> GetTemplatesAsync(
		Guid academicYearId,
		GetAdmissionCrmRegistrationTemplateListInput input)
	{
		await EnsureYearAccessAsync(academicYearId, nameof(GetTemplatesAsync));
		var queryable = await _templateRepository.GetQueryableAsync();
		var templates = await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId &&
			(!input.Status.HasValue || x.Status == input.Status.Value) &&
			(string.IsNullOrWhiteSpace(input.Filter) || x.Name.Contains(input.Filter))));
		var ordered = templates.OrderBy(x => x.Name).ThenByDescending(x => x.Version).ToList();
		return new PagedResultDto<AdmissionCrmRegistrationTemplateDto>(
			ordered.Count,
			ordered.Skip(input.SkipCount).Take(input.MaxResultCount).Select(ToTemplateDto).ToList());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<AdmissionCrmRegistrationTemplateDto> GetTemplateAsync(Guid id)
	{
		return ToTemplateDto(await GetTemplateForTenantAsync(id, nameof(GetTemplateAsync)));
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRegistrationTemplates)]
	public async Task<AdmissionCrmRegistrationTemplateDto> CreateTemplateAsync(
		Guid academicYearId,
		CreateAdmissionCrmRegistrationTemplateDto input)
	{
		await _studentScopeAccessService.EnsureAcademicYearWideConfigurationAccessAsync(
			academicYearId,
			nameof(CreateTemplateAsync));
		NormalizeAndValidate(input);
		var queryable = await _templateRepository.GetQueryableAsync();
		var existing = await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId && x.Name == input.Name.Trim()));
		var version = existing.Count == 0 ? 1 : existing.Max(x => x.Version) + 1;
		var template = CreateTemplateEntity(academicYearId, version, input);
		await _templateRepository.InsertAsync(template, autoSave: true);
		return ToTemplateDto(template);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRegistrationTemplates)]
	public async Task<AdmissionCrmRegistrationTemplateDto> UpdateTemplateAsync(
		Guid id,
		UpdateAdmissionCrmRegistrationTemplateDto input)
	{
		var template = await GetTemplateForTenantAsync(id, nameof(UpdateTemplateAsync));
		await _studentScopeAccessService.EnsureAcademicYearWideConfigurationAccessAsync(
			template.AcademicYearId,
			nameof(UpdateTemplateAsync));
		NormalizeAndValidate(input);
		ApplyTemplateUpdate(template, input);
		await _templateRepository.UpdateAsync(template, autoSave: true);
		return ToTemplateDto(template);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.PublishRegistrationTemplates)]
	public async Task<AdmissionCrmRegistrationTemplateDto> PublishTemplateAsync(Guid id)
	{
		var template = await GetTemplateForTenantAsync(id, nameof(PublishTemplateAsync));
		await _studentScopeAccessService.EnsureAcademicYearWideConfigurationAccessAsync(
			template.AcademicYearId,
			nameof(PublishTemplateAsync));
		var input = ToTemplateUpsert(template);
		NormalizeAndValidate(input);
		await EnsureNoAmbiguousPublishedTemplateAsync(template);
		template.Publish(Clock.Now);
		await _templateRepository.UpdateAsync(template, autoSave: true);
		return ToTemplateDto(template);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRegistrationTemplates)]
	public async Task<AdmissionCrmRegistrationTemplateDto> CreateTemplateVersionAsync(Guid id)
	{
		var source = await GetTemplateForTenantAsync(id, nameof(CreateTemplateVersionAsync));
		await _studentScopeAccessService.EnsureAcademicYearWideConfigurationAccessAsync(
			source.AcademicYearId,
			nameof(CreateTemplateVersionAsync));
		var queryable = await _templateRepository.GetQueryableAsync();
		var versions = await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == source.AcademicYearId && x.Name == source.Name));
		var draft = CreateTemplateEntity(source.AcademicYearId, versions.Max(x => x.Version) + 1, ToTemplateUpsert(source));
		await _templateRepository.InsertAsync(draft, autoSave: true);
		return ToTemplateDto(draft);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.PublishRegistrationTemplates)]
	public async Task<AdmissionCrmRegistrationTemplateDto> RetireTemplateAsync(Guid id)
	{
		var template = await GetTemplateForTenantAsync(id, nameof(RetireTemplateAsync));
		await _studentScopeAccessService.EnsureAcademicYearWideConfigurationAccessAsync(
			template.AcademicYearId,
			nameof(RetireTemplateAsync));
		template.Retire(Clock.Now);
		await _templateRepository.UpdateAsync(template, autoSave: true);
		return ToTemplateDto(template);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<PagedResultDto<AdmissionCrmFormNumberSeriesDto>> GetFormNumberSeriesAsync(Guid academicYearId)
	{
		await EnsureYearAccessAsync(academicYearId, nameof(GetFormNumberSeriesAsync));
		var queryable = await _seriesRepository.GetQueryableAsync();
		var items = await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId));
		return new PagedResultDto<AdmissionCrmFormNumberSeriesDto>(items.Count, items.OrderBy(x => x.Name).Select(ToSeriesDto).ToList());
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageFormNumberSeries)]
	public async Task<AdmissionCrmFormNumberSeriesDto> CreateFormNumberSeriesAsync(
		Guid academicYearId,
		AdmissionCrmFormNumberSeriesUpsertDto input)
	{
		await _studentScopeAccessService.EnsureAcademicYearWideConfigurationAccessAsync(
			academicYearId,
			nameof(CreateFormNumberSeriesAsync));
		var series = new AdmissionCrmFormNumberSeries(
			GuidGenerator.Create(), CurrentTenant.Id, academicYearId, input.Name, input.Prefix, input.Suffix,
			input.Padding, input.NextNumber, Serialize(input.Applicability), input.IsActive);
		await _seriesRepository.InsertAsync(series, autoSave: true);
		return ToSeriesDto(series);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageFormNumberSeries)]
	public async Task<AdmissionCrmFormNumberSeriesDto> UpdateFormNumberSeriesAsync(
		Guid id,
		AdmissionCrmFormNumberSeriesUpsertDto input)
	{
		var series = await GetSeriesForTenantAsync(id, nameof(UpdateFormNumberSeriesAsync));
		await _studentScopeAccessService.EnsureAcademicYearWideConfigurationAccessAsync(
			series.AcademicYearId,
			nameof(UpdateFormNumberSeriesAsync));
		series.Update(input.Name, input.Prefix, input.Suffix, input.Padding, input.NextNumber, Serialize(input.Applicability), input.IsActive);
		await _seriesRepository.UpdateAsync(series, autoSave: true);
		return ToSeriesDto(series);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.IssueRegistrationForm)]
	public async Task<AdmissionCrmRegistrationApplicationDto> IssueAsync(
		Guid leadId,
		IssueAdmissionCrmRegistrationApplicationDto input)
	{
		return await IssueCoreAsync(
			leadId,
			input,
			isDirectRegistration: false,
			AdmissionCrmRegistrationProvenance.OnlineParent);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.IssueRegistrationForm)]
	public async Task<AdmissionCrmRegistrationApplicationDto> IssueDirectAsync(
		Guid leadId,
		IssueAdmissionCrmRegistrationApplicationDto input)
	{
		return await IssueCoreAsync(
			leadId,
			input,
			isDirectRegistration: true,
			AdmissionCrmRegistrationProvenance.StaffAssistedCounter);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.IssueRegistrationForm)]
	public async Task<AdmissionCrmRegistrationApplicationDto> IssueFromPaperAsync(
		Guid leadId,
		CreatePaperAdmissionCrmRegistrationDto input)
	{
		var registration = await IssueCoreAsync(
			leadId,
			input.Registration,
			isDirectRegistration: true,
			AdmissionCrmRegistrationProvenance.StaffEnteredFromPaper);
		var application = await GetApplicationForTenantAsync(registration.Id, nameof(IssueFromPaperAsync));
		application.RecordPaperReceipt(
			input.PhysicalFormNumber,
			input.PhysicalFormReceivedDate == default ? Clock.Now.Date : input.PhysicalFormReceivedDate,
			input.PhysicalFormReceivedByUserId ?? CurrentUser.Id ?? throw new BusinessException("AdmissionCrm:PaperRegistrationRequiresStaffUser"),
			input.ScannedFormAttachmentReference);
		await _applicationRepository.UpdateAsync(application, autoSave: true);
		await AddApplicationTimelineAsync(
			application,
			AdmissionCrmTimelineEventType.PaperRegistrationEntered,
			"Paper registration entered",
			$"Physical form {application.PhysicalFormNumber} received on {application.PhysicalFormReceivedDate:yyyy-MM-dd}.");
		return ToApplicationDto(application);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.IssueRegistrationForm)]
	public async Task EnsureStandardIssuanceEligibleAsync(Guid leadId, Guid? playbookId = null)
	{
		var lead = await GetLeadForTenantAsync(
			leadId,
			nameof(EnsureStandardIssuanceEligibleAsync));
		var registrationPath = await ResolveRegistrationPathAsync(lead, playbookId);
		await EnsureIssuanceEligibleAsync(
			lead,
			registrationPath,
			isDirectRegistration: false);
	}

	private async Task<AdmissionCrmRegistrationApplicationDto> IssueCoreAsync(
		Guid leadId,
		IssueAdmissionCrmRegistrationApplicationDto input,
		bool isDirectRegistration,
		AdmissionCrmRegistrationProvenance provenance)
	{
		if (input.WaiveFee)
		{
			await AuthorizationService.CheckAsync(
				ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.WaiveRegistrationFee);
		}

		var lead = await GetLeadForTenantAsync(leadId, nameof(IssueAsync));
		var template = await GetTemplateForTenantAsync(input.RegistrationTemplateId, nameof(IssueAsync));
		if (template.AcademicYearId != lead.AcademicYearId || template.Status != AdmissionCrmRegistrationTemplateStatus.Published)
		{
			throw new BusinessException("AdmissionCrm:PublishedRegistrationTemplateRequired");
		}

		var registrationPath = isDirectRegistration && !input.PlaybookId.HasValue
			? AdmissionCrmRegistrationPath.DirectRegistration
			: await ResolveRegistrationPathAsync(lead, input.PlaybookId);
		await EnsureIssuanceEligibleAsync(lead, registrationPath, isDirectRegistration);

		var applicationQueryable = await _applicationRepository.GetQueryableAsync();
		var existing = await AsyncExecuter.FirstOrDefaultAsync(applicationQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == leadId && x.Status != AdmissionCrmRegistrationStatus.Revoked));
		if (existing != null)
		{
			throw new BusinessException("AdmissionCrm:RegistrationApplicationAlreadyIssued").WithData("ApplicationId", existing.Id);
		}

		var formNumber = await ResolveFormNumberAsync(lead, input);
		await _studentEnrollmentManager.ValidateFormNumberUniqueAsync(formNumber, lead.AcademicYearId);
		await EnsureApplicationFormNumberUniqueAsync(formNumber, lead.AcademicYearId);
		var expiryDays = input.LinkExpiryDays ?? template.LinkExpiryDays;
		if (expiryDays is < 1 or > 365)
		{
			throw new BusinessException("AdmissionCrm:InvalidRegistrationLinkExpiryDays");
		}

		var now = Clock.Now;
		var prefill = BuildPrefill(lead);
		var application = new AdmissionCrmRegistrationApplication(
			GuidGenerator.Create(), CurrentTenant.Id, lead.AcademicYearId, lead.Id, template.Id, template.Version,
			input.PlaybookId, formNumber, Serialize(ToTemplateDto(template)), Serialize(prefill), now,
			now.AddDays(expiryDays), template.FeeRequired ? template.FeeAmount : 0, provenance);
		if (input.WaiveFee)
		{
			application.WaiveFee(Check.NotNullOrWhiteSpace(input.WaiverReason, nameof(input.WaiverReason), AdmissionCrmConsts.MaxNotesLength));
		}
		await _applicationRepository.InsertAsync(application, autoSave: false);
		var statusPath = await EnsureApplicantStatusLinkAsync(lead, now.AddDays(Math.Max(expiryDays, 90)));
		if (!lead.RegistrationFormIssuedAt.HasValue)
		{
			lead.IssueRegistrationForm(formNumber, decimal.ToDouble(template.FeeAmount), now);
			await _leadRepository.UpdateAsync(lead, autoSave: false);
		}
		await AddTimelineAsync(lead, AdmissionCrmTimelineEventType.RegistrationFormIssued, "Registration form issued", $"Form {formNumber}, template {template.Name} v{template.Version}.", application.Id);
		await CurrentUnitOfWork!.SaveChangesAsync();
		await _documentChecklistWorkflow.AssignApplicableChecklistFromAutomationAsync(
			application.Id,
			new AdmissionCrmPlaybookApplicabilityDto
			{
				Campus = lead.CampusBranchName,
				DesiredClass = lead.DesiredClassName
			});
		var dto = ToApplicationDto(application);
		dto.ApplicantStatusPath = statusPath;
		return dto;
	}

	public async Task<bool> IssueApplicableFromAutomationAsync(Guid leadId, AdmissionInquiryStage stage)
	{
		var lead = await GetLeadForTenantAsync(leadId, nameof(IssueApplicableFromAutomationAsync));
		var applicationQueryable = await _applicationRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(applicationQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == leadId && x.Status != AdmissionCrmRegistrationStatus.Revoked)))
		{
			return false;
		}

		var templateQueryable = await _templateRepository.GetQueryableAsync();
		var candidates = await AsyncExecuter.ToListAsync(templateQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == lead.AcademicYearId &&
			x.Status == AdmissionCrmRegistrationTemplateStatus.Published &&
			x.AutoIssueAtStage == stage));
		var actual = new AdmissionCrmPlaybookApplicabilityDto
		{
			Campus = lead.CampusBranchName,
			DesiredClass = lead.DesiredClassName
		};
		var template = candidates
			.Select(item => new { Item = item, Scope = Deserialize<AdmissionCrmPlaybookApplicabilityDto>(item.ApplicabilityJson) })
			.Where(item => MatchesApplicability(item.Scope, actual))
			.OrderByDescending(item => ApplicabilityScore(item.Scope))
			.ThenByDescending(item => item.Item.IsFallback)
			.Select(item => item.Item)
			.FirstOrDefault();
		if (template == null)
		{
			return false;
		}

		await IssueCoreAsync(leadId, new IssueAdmissionCrmRegistrationApplicationDto
		{
			RegistrationTemplateId = template.Id
		}, isDirectRegistration: false, AdmissionCrmRegistrationProvenance.OnlineParent);
		return true;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<AdmissionCrmRegistrationApplicationDto> GetApplicationAsync(Guid id)
	{
		var application = await GetApplicationForTenantAsync(id, nameof(GetApplicationAsync));
		var dto = ToApplicationDto(application);
		dto.ApplicantStatusPath = await GetExistingApplicantStatusPathAsync(application.LeadId);
		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public async Task<AdmissionCrmRegistrationApplicationDto> GetCurrentApplicationByLeadAsync(Guid leadId)
	{
		await GetLeadForTenantAsync(leadId, nameof(GetCurrentApplicationByLeadAsync));
		var queryable = await _applicationRepository.GetQueryableAsync();
		var applications = await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == leadId));
		var application = applications
			.OrderBy(x => x.Status == AdmissionCrmRegistrationStatus.Revoked)
			.ThenByDescending(x => x.IssuedAt)
			.FirstOrDefault();
		if (application == null)
			throw new BusinessException("AdmissionCrm:RegistrationApplicationNotFound");
		var dto = ToApplicationDto(application);
		dto.ApplicantStatusPath = await GetExistingApplicantStatusPathAsync(application.LeadId);
		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.IssueRegistrationForm)]
	public async Task<AdmissionCrmRegistrationApplicationDto> SaveStaffResponsesAsync(
		Guid id,
		SaveAdmissionCrmRegistrationResponsesDto input)
	{
		var application = await GetApplicationForTenantAsync(id, nameof(SaveStaffResponsesAsync));
		application.EnsureResponseVersion(input.ConcurrencyStamp);
		var template = AdmissionCrmRegistrationResponseValidator.DeserializeTemplate(
			application.TemplateSnapshotJson);
		if (!template.AllowPartialSave)
		{
			throw new BusinessException("AdmissionCrm:RegistrationPartialSaveDisabled");
		}
		var completion = AdmissionCrmRegistrationResponseValidator.Validate(
			application,
			template,
			input.Responses,
			requireComplete: false,
			allowStaffManagedFields: true);
		application.Save(Serialize(input.Responses), completion, Clock.Now);
		await _applicationRepository.UpdateAsync(application, autoSave: true);
		await AddApplicationTimelineAsync(
			application,
			AdmissionCrmTimelineEventType.RegistrationSaved,
			"Offline registration saved by staff",
			$"Completion: {completion:0.##}%.");
		return ToApplicationDto(application);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.IssueRegistrationForm)]
	public async Task<AdmissionCrmRegistrationApplicationDto> SubmitStaffResponsesAsync(
		Guid id,
		SaveAdmissionCrmRegistrationResponsesDto input)
	{
		var application = await GetApplicationForTenantAsync(id, nameof(SubmitStaffResponsesAsync));
		application.EnsureResponseVersion(input.ConcurrencyStamp);
		var template = AdmissionCrmRegistrationResponseValidator.DeserializeTemplate(
			application.TemplateSnapshotJson);
		AdmissionCrmRegistrationResponseValidator.Validate(
			application,
			template,
			input.Responses,
			requireComplete: true,
			allowStaffManagedFields: true);
		var isResubmission = application.Status == AdmissionCrmRegistrationStatus.ReturnedForCorrection;
		application.Submit(Serialize(input.Responses), Clock.Now, isResubmission);
		await _applicationRepository.UpdateAsync(application, autoSave: true);
		await AddApplicationTimelineAsync(
			application,
			AdmissionCrmTimelineEventType.RegistrationSubmitted,
			isResubmission
				? "Offline registration corrections submitted by staff"
				: "Offline registration submitted by staff",
			"Entered from a paper form by an authorized staff member.");
		return ToApplicationDto(application);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.VerifyRegistration)]
	public async Task<AdmissionCrmRegistrationApplicationDto> ReturnForCorrectionAsync(
		Guid id,
		ReturnAdmissionCrmRegistrationForCorrectionDto input)
	{
		var application = await GetApplicationForTenantAsync(id, nameof(ReturnForCorrectionAsync));
		var validKeys = GetFieldKeys(application);
		var keys = input.FieldKeys.Select(x => x.Trim()).Distinct(CodeComparer).ToList();
		if (keys.Count == 0 || keys.Any(x => !validKeys.Contains(x)))
		{
			throw new BusinessException("AdmissionCrm:InvalidRegistrationCorrectionFields");
		}
		application.ReturnForCorrection(Serialize(keys), input.Reason);
		await _applicationRepository.UpdateAsync(application, autoSave: true);
		await AddApplicationTimelineAsync(application, AdmissionCrmTimelineEventType.RegistrationReturnedForCorrection, "Registration returned for correction", input.Reason);
		return ToApplicationDto(application);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.VerifyRegistration)]
	public async Task<AdmissionCrmRegistrationApplicationDto> VerifyAsync(Guid id)
	{
		var application = await GetApplicationForTenantAsync(id, nameof(VerifyAsync));
		application.Verify(Clock.Now);
		await _applicationRepository.UpdateAsync(application, autoSave: true);
		await AddApplicationTimelineAsync(application, AdmissionCrmTimelineEventType.RegistrationVerified, "Registration verified", null);
		return ToApplicationDto(application);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRegistrationLinks)]
	public async Task<AdmissionCrmRegistrationApplicationDto> ExtendAsync(Guid id, ExtendAdmissionCrmRegistrationLinkDto input)
	{
		var application = await GetApplicationForTenantAsync(id, nameof(ExtendAsync));
		application.Extend(input.NewExpiry, input.Reason, Clock.Now);
		await _applicationRepository.UpdateAsync(application, autoSave: true);
		await AddApplicationTimelineAsync(application, AdmissionCrmTimelineEventType.RegistrationLinkExtended, "Registration link extended", input.Reason);
		return ToApplicationDto(application);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRegistrationLinks)]
	public async Task<AdmissionCrmRegistrationApplicationDto> RevokeAsync(Guid id, AdmissionCrmRegistrationReasonDto input)
	{
		var application = await GetApplicationForTenantAsync(id, nameof(RevokeAsync));
		application.Revoke(input.Reason, Clock.Now);
		await _applicationRepository.UpdateAsync(application, autoSave: true);
		await AddApplicationTimelineAsync(application, AdmissionCrmTimelineEventType.RegistrationLinkRevoked, "Registration link revoked", input.Reason);
		return ToApplicationDto(application);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRegistrationLinks)]
	public async Task<AdmissionCrmRegistrationApplicationDto> ReissueAsync(
		Guid id,
		AdmissionCrmRegistrationReissueDto input)
	{
		var application = await GetApplicationForTenantAsync(id, nameof(ReissueAsync));
		var now = Clock.Now;
		application.Reissue(now.AddDays(input.LinkExpiryDays), input.Reason, now);
		await _applicationRepository.UpdateAsync(application, autoSave: false);
		var lead = await GetLeadForTenantAsync(application.LeadId, nameof(ReissueAsync));
		var statusPath = await ForceReissueApplicantStatusLinkAsync(lead, now.AddDays(Math.Max(input.LinkExpiryDays, 90)));
		await AddTimelineAsync(
			lead,
			AdmissionCrmTimelineEventType.RegistrationLinkReissued,
			"Registration link reissued",
			input.Reason,
			application.Id);
		await CurrentUnitOfWork!.SaveChangesAsync();
		var dto = ToApplicationDto(application);
		dto.ApplicantStatusPath = statusPath;
		return dto;
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.IssueRegistrationForm)]
	public async Task<AdmissionCrmRegistrationApplicationDto> RecordPaymentReferenceAsync(
		Guid id,
		RecordAdmissionCrmRegistrationPaymentReferenceDto input)
	{
		var application = await GetApplicationForTenantAsync(id, nameof(RecordPaymentReferenceAsync));
		application.RecordPaymentReference(input.PaymentReference);
		await _applicationRepository.UpdateAsync(application, autoSave: true);
		await AddApplicationTimelineAsync(application, AdmissionCrmTimelineEventType.RegistrationPaymentReferenceRecorded, "Registration payment reference recorded", input.PaymentReference);
		return ToApplicationDto(application);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.WaiveRegistrationFee)]
	public async Task<AdmissionCrmRegistrationApplicationDto> WaiveFeeAsync(Guid id, AdmissionCrmRegistrationReasonDto input)
	{
		var application = await GetApplicationForTenantAsync(id, nameof(WaiveFeeAsync));
		application.WaiveFee(input.Reason);
		await _applicationRepository.UpdateAsync(application, autoSave: true);
		await AddApplicationTimelineAsync(application, AdmissionCrmTimelineEventType.RegistrationFeeWaived, "Registration fee waived", input.Reason);
		return ToApplicationDto(application);
	}

	private static void NormalizeAndValidate(AdmissionCrmRegistrationTemplateUpsertDto input)
	{
		input.Name = Check.NotNullOrWhiteSpace(input.Name, nameof(input.Name), AdmissionCrmConsts.MaxTemplateNameLength).Trim();
		if (input.IsFallback && HasApplicability(input.Applicability))
		{
			throw new BusinessException("AdmissionCrm:FallbackRegistrationTemplateCannotHaveApplicability");
		}
		if (input.Sections.Count == 0)
		{
			throw new BusinessException("AdmissionCrm:RegistrationTemplateRequiresSection");
		}
		var sectionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var fieldKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var section in input.Sections)
		{
			section.Key = Check.NotNullOrWhiteSpace(section.Key, nameof(section.Key), 80).Trim();
			section.Title = Check.NotNullOrWhiteSpace(section.Title, nameof(section.Title), 120).Trim();
			section.LocalizedTitles = NormalizeLocalizations(section.LocalizedTitles, 120);
			section.LocalizedDescriptions = NormalizeLocalizations(section.LocalizedDescriptions, 500);
			if (!sectionKeys.Add(section.Key) || section.Fields.Count == 0)
			{
				throw new BusinessException("AdmissionCrm:InvalidRegistrationTemplateSection").WithData("SectionKey", section.Key);
			}
			foreach (var field in section.Fields)
			{
				field.Key = Check.NotNullOrWhiteSpace(field.Key, nameof(field.Key), 80).Trim();
				field.Label = Check.NotNullOrWhiteSpace(field.Label, nameof(field.Label), 120).Trim();
				if (!fieldKeys.Add(field.Key))
				{
					throw new BusinessException("AdmissionCrm:DuplicateRegistrationFieldKey").WithData("FieldKey", field.Key);
				}
				if (!string.IsNullOrWhiteSpace(field.SystemField) && !SupportedSystemFields.Contains(field.SystemField))
				{
					throw new BusinessException("AdmissionCrm:UnsupportedRegistrationSystemField").WithData("SystemField", field.SystemField);
				}
				if (!Enum.IsDefined(field.FieldType))
				{
					throw new BusinessException("AdmissionCrm:InvalidRegistrationFieldType").WithData("FieldKey", field.Key);
				}
				var requiresOptions =
					field.FieldType is AdmissionCrmFormFieldType.Select or AdmissionCrmFormFieldType.MultiSelect;
				if ((requiresOptions && field.Options.Count == 0) ||
				    (!requiresOptions && field.Options.Count > 0))
				{
					throw new BusinessException("AdmissionCrm:InvalidRegistrationFieldOptions").WithData("FieldKey", field.Key);
				}
				field.LocalizedLabels = NormalizeLocalizations(field.LocalizedLabels, 120);
				field.LocalizedHelpTexts = NormalizeLocalizations(field.LocalizedHelpTexts, 300);
				field.LocalizedOptions = NormalizeLocalizedOptions(field);
				if (!string.IsNullOrWhiteSpace(field.ValidationPattern))
				{
					try { _ = new Regex(field.ValidationPattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250)); }
					catch (ArgumentException) { throw new BusinessException("AdmissionCrm:InvalidRegistrationValidationPattern").WithData("FieldKey", field.Key); }
				}
				if (field.MinimumLength > field.MaximumLength || field.MinimumValue > field.MaximumValue)
				{
					throw new BusinessException("AdmissionCrm:InvalidRegistrationFieldValidation").WithData("FieldKey", field.Key);
				}
			}
		}
		foreach (var field in input.Sections.SelectMany(x => x.Fields))
		{
			if ((!string.IsNullOrWhiteSpace(field.VisibleWhenFieldKey) && !fieldKeys.Contains(field.VisibleWhenFieldKey)) ||
			    (!string.IsNullOrWhiteSpace(field.RequiredWhenFieldKey) && !fieldKeys.Contains(field.RequiredWhenFieldKey)))
			{
				throw new BusinessException("AdmissionCrm:RegistrationFieldConditionTargetNotFound").WithData("FieldKey", field.Key);
			}
		}
	}

	private static IReadOnlyDictionary<string, string> NormalizeLocalizations(
		IReadOnlyDictionary<string, string> values,
		int maximumValueLength)
	{
		var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		foreach (var (language, value) in values)
		{
			var normalizedLanguage = Check.NotNullOrWhiteSpace(language, nameof(language), 16)
				.Trim()
				.ToLowerInvariant();
			var normalizedValue = Check.NotNullOrWhiteSpace(value, nameof(value), maximumValueLength).Trim();
			normalized[normalizedLanguage] = normalizedValue;
		}

		return normalized;
	}

	private static IReadOnlyDictionary<string, IReadOnlyList<string>> NormalizeLocalizedOptions(
		AdmissionCrmRegistrationFieldDto field)
	{
		var normalized = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
		foreach (var (language, values) in field.LocalizedOptions)
		{
			var normalizedLanguage = Check.NotNullOrWhiteSpace(language, nameof(language), 16)
				.Trim()
				.ToLowerInvariant();
			var normalizedValues = values
				.Select(value => Check.NotNullOrWhiteSpace(value, nameof(value), 160).Trim())
				.ToList();
			if (normalizedValues.Count != field.Options.Count)
			{
				throw new BusinessException("AdmissionCrm:RegistrationLocalizedOptionsCountMismatch")
					.WithData("FieldKey", field.Key)
					.WithData("Language", normalizedLanguage);
			}
			normalized[normalizedLanguage] = normalizedValues;
		}

		return normalized;
	}

	private async Task<string> ResolveFormNumberAsync(AdmissionInquiryLead lead, IssueAdmissionCrmRegistrationApplicationDto input)
	{
		if (!string.IsNullOrWhiteSpace(input.ManualFormNumber))
		{
			return input.ManualFormNumber.Trim().ToUpperInvariant();
		}
		AdmissionCrmFormNumberSeries series;
		if (input.FormNumberSeriesId.HasValue)
		{
			series = await GetSeriesForTenantAsync(input.FormNumberSeriesId.Value, nameof(IssueAsync));
		}
		else
		{
			var queryable = await _seriesRepository.GetQueryableAsync();
			var candidates = await AsyncExecuter.ToListAsync(queryable.Where(x =>
				x.TenantId == CurrentTenant.Id && x.AcademicYearId == lead.AcademicYearId && x.IsActive));
			var actual = new AdmissionCrmPlaybookApplicabilityDto
			{
				Campus = lead.CampusBranchName,
				DesiredClass = lead.DesiredClassName
			};
			series = candidates
				.Select(item => new { Item = item, Scope = Deserialize<AdmissionCrmPlaybookApplicabilityDto>(item.ApplicabilityJson) })
				.Where(item => MatchesApplicability(item.Scope, actual))
				.OrderByDescending(item => ApplicabilityScore(item.Scope))
				.Select(item => item.Item)
				.FirstOrDefault()
				?? throw new BusinessException("AdmissionCrm:FormNumberSeriesOrManualNumberRequired");
		}
		if (series.AcademicYearId != lead.AcademicYearId)
		{
			throw new BusinessException("AdmissionCrm:FormNumberSeriesAcademicYearMismatch");
		}
		var number = series.TakeNextNumber();
		await _seriesRepository.UpdateAsync(series, autoSave: true);
		return number;
	}

	private async Task<string> EnsureApplicantStatusLinkAsync(AdmissionInquiryLead lead, DateTime expiresAt)
	{
		var queryable = await _statusAccessRepository.GetQueryableAsync();
		var access = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == lead.Id));
		string rawToken;
		if (access == null)
		{
			rawToken = CreateRawToken();
			access = new AdmissionApplicantStatusAccess(
				GuidGenerator.Create(), CurrentTenant.Id, lead.Id, HashSecret(rawToken), EncryptRequired(rawToken), expiresAt, Clock.Now);
			await _statusAccessRepository.InsertAsync(access, autoSave: false);
		}
		else
		{
			rawToken = string.IsNullOrWhiteSpace(access.EncryptedToken)
				? string.Empty
				: _stringEncryptionService.Decrypt(access.EncryptedToken) ?? string.Empty;
			if (string.IsNullOrWhiteSpace(rawToken) || access.TokenExpiresAt <= Clock.Now)
			{
				rawToken = CreateRawToken();
				access.ReissueLink(HashSecret(rawToken), EncryptRequired(rawToken), expiresAt, Clock.Now);
				await _statusAccessRepository.UpdateAsync(access, autoSave: false);
			}
		}
		return $"/student/admissions/status/{CurrentTenant.Id:D}/{rawToken}";
	}

	private async Task<string?> GetExistingApplicantStatusPathAsync(Guid leadId)
	{
		if (!await AuthorizationService.IsGrantedAsync(
			    ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRegistrationLinks))
		{
			return null;
		}

		var queryable = await _statusAccessRepository.GetQueryableAsync();
		var access = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == leadId));
		if (access == null ||
		    access.TokenExpiresAt <= Clock.Now ||
		    string.IsNullOrWhiteSpace(access.EncryptedToken))
		{
			return null;
		}

		var rawToken = _stringEncryptionService.Decrypt(access.EncryptedToken);
		return string.IsNullOrWhiteSpace(rawToken)
			? null
			: $"/student/admissions/status/{CurrentTenant.Id:D}/{rawToken}";
	}

	private async Task<string> ForceReissueApplicantStatusLinkAsync(AdmissionInquiryLead lead, DateTime expiresAt)
	{
		var queryable = await _statusAccessRepository.GetQueryableAsync();
		var access = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.LeadId == lead.Id));
		var rawToken = CreateRawToken();
		if (access == null)
		{
			access = new AdmissionApplicantStatusAccess(
				GuidGenerator.Create(), CurrentTenant.Id, lead.Id, HashSecret(rawToken), EncryptRequired(rawToken), expiresAt, Clock.Now);
			await _statusAccessRepository.InsertAsync(access, autoSave: false);
		}
		else
		{
			access.ReissueLink(HashSecret(rawToken), EncryptRequired(rawToken), expiresAt, Clock.Now);
			await _statusAccessRepository.UpdateAsync(access, autoSave: false);
		}
		return $"/student/admissions/status/{CurrentTenant.Id:D}/{rawToken}";
	}

	private async Task EnsureNoAmbiguousPublishedTemplateAsync(AdmissionCrmRegistrationTemplate candidate)
	{
		var queryable = await _templateRepository.GetQueryableAsync();
		var published = await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == candidate.AcademicYearId &&
			x.Status == AdmissionCrmRegistrationTemplateStatus.Published && x.Id != candidate.Id));
		var candidateScope = Deserialize<AdmissionCrmPlaybookApplicabilityDto>(candidate.ApplicabilityJson);
		if (published.Any(x => SameApplicability(candidateScope, Deserialize<AdmissionCrmPlaybookApplicabilityDto>(x.ApplicabilityJson))))
		{
			throw new BusinessException("AdmissionCrm:AmbiguousPublishedRegistrationTemplate");
		}
	}

	private async Task EnsureApplicationFormNumberUniqueAsync(string formNumber, Guid academicYearId)
	{
		var queryable = await _applicationRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId && x.FormNumber == formNumber)))
		{
			throw new BusinessException("AdmissionCrm:RegistrationFormNumberAlreadyExists").WithData("FormNumber", formNumber);
		}
	}

	private async Task<AdmissionCrmRegistrationPath> ResolveRegistrationPathAsync(
		AdmissionInquiryLead lead,
		Guid? playbookId)
	{
		if (playbookId.HasValue)
		{
			var playbook = await _playbookRepository.FindAsync(playbookId.Value);
			if (playbook == null || playbook.TenantId != CurrentTenant.Id)
			{
				throw new BusinessException("AdmissionCrm:PlaybookNotFound")
					.WithData("PlaybookId", playbookId.Value);
			}

			if (playbook.AcademicYearId != lead.AcademicYearId)
			{
				throw new BusinessException("AdmissionCrm:PlaybookAcademicYearMismatch")
					.WithData("PlaybookId", playbook.Id)
					.WithData("LeadAcademicYearId", lead.AcademicYearId)
					.WithData("PlaybookAcademicYearId", playbook.AcademicYearId);
			}

			if (playbook.Status != AdmissionCrmPlaybookStatus.Published)
			{
				throw new BusinessException("AdmissionCrm:PublishedPlaybookRequired")
					.WithData("PlaybookId", playbook.Id);
			}

			return playbook.RegistrationPath;
		}

		var cycleQueryable = await _cycleRepository.GetQueryableAsync();
		var configuration = await AsyncExecuter.FirstOrDefaultAsync(cycleQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == lead.AcademicYearId));
		if (configuration?.EntranceExamRequired == false)
		{
			return AdmissionCrmRegistrationPath.ExamWaived;
		}

		return configuration?.RegistrationFormRequiredBeforeExam == true
			? AdmissionCrmRegistrationPath.RegistrationBeforeExam
			: AdmissionCrmRegistrationPath.RegistrationAfterExam;
	}

	private async Task EnsureIssuanceEligibleAsync(
		AdmissionInquiryLead lead,
		AdmissionCrmRegistrationPath registrationPath,
		bool isDirectRegistration)
	{
		if (isDirectRegistration)
		{
			if (registrationPath != AdmissionCrmRegistrationPath.DirectRegistration)
			{
				throw new BusinessException("AdmissionCrm:DirectRegistrationPlaybookRequired")
					.WithData("LeadId", lead.Id)
					.WithData("RegistrationPath", registrationPath);
			}

			return;
		}

		if (registrationPath == AdmissionCrmRegistrationPath.DirectRegistration)
		{
			throw new BusinessException("AdmissionCrm:DirectRegistrationPathRequiresDirectEndpoint")
				.WithData("LeadId", lead.Id);
		}

		if (!lead.OwnerUserId.HasValue)
		{
			throw new BusinessException("AdmissionCrm:RegistrationOwnerRequired")
				.WithData("LeadId", lead.Id);
		}

		if (lead.Stage is AdmissionInquiryStage.Rejected
		    or AdmissionInquiryStage.Stale
		    or AdmissionInquiryStage.Archived)
		{
			throw new BusinessException("AdmissionCrm:RegistrationNotAllowedForTerminalLead")
				.WithData("LeadId", lead.Id)
				.WithData("Stage", lead.Stage);
		}

		if (lead.Stage == AdmissionInquiryStage.NewInquiry)
		{
			throw new BusinessException("AdmissionCrm:RegistrationFollowUpRequired")
				.WithData("LeadId", lead.Id);
		}

		switch (registrationPath)
		{
			case AdmissionCrmRegistrationPath.RegistrationBeforeExam:
				if (lead.Stage is not (AdmissionInquiryStage.FollowUp or AdmissionInquiryStage.ExamReady))
				{
					throw new BusinessException("AdmissionCrm:RegistrationMustBeIssuedBeforeExam")
						.WithData("LeadId", lead.Id)
						.WithData("Stage", lead.Stage);
				}
				return;

			case AdmissionCrmRegistrationPath.RegistrationAfterExam:
				var examQueryable = await _examRepository.GetQueryableAsync();
				var hasEligibleExamResult = await AsyncExecuter.AnyAsync(examQueryable.Where(x =>
					x.TenantId == CurrentTenant.Id &&
					x.AcademicYearId == lead.AcademicYearId &&
					x.LeadId == lead.Id &&
					x.ResultRecordedAt.HasValue &&
					x.EligibilityDecision == AdmissionCrmEligibilityDecision.Eligible));
				if (!hasEligibleExamResult || lead.Stage != AdmissionInquiryStage.Eligible)
				{
					throw new BusinessException("AdmissionCrm:RegistrationRequiresEligibleExamResult")
						.WithData("LeadId", lead.Id)
						.WithData("Stage", lead.Stage);
				}
				return;

			case AdmissionCrmRegistrationPath.ExamWaived:
				if (lead.Stage is not (AdmissionInquiryStage.FollowUp
				    or AdmissionInquiryStage.ExamReady
				    or AdmissionInquiryStage.Eligible))
				{
					throw new BusinessException("AdmissionCrm:RegistrationStageNotEligible")
						.WithData("LeadId", lead.Id)
						.WithData("Stage", lead.Stage);
				}
				return;

			default:
				throw new BusinessException("AdmissionCrm:InvalidRegistrationPath")
					.WithData("RegistrationPath", registrationPath);
		}
	}

	private async Task EnsureYearAccessAsync(Guid academicYearId, string operation)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(academicYearId, operation);
	}

	private async Task<AdmissionCrmRegistrationTemplate> GetTemplateForTenantAsync(Guid id, string operation)
	{
		var template = await _templateRepository.GetAsync(id);
		if (template.TenantId != CurrentTenant.Id) throw new BusinessException("AdmissionCrm:RegistrationTemplateNotFound");
		await EnsureYearAccessAsync(template.AcademicYearId, operation);
		return template;
	}

	private async Task<AdmissionCrmFormNumberSeries> GetSeriesForTenantAsync(Guid id, string operation)
	{
		var series = await _seriesRepository.GetAsync(id);
		if (series.TenantId != CurrentTenant.Id) throw new BusinessException("AdmissionCrm:FormNumberSeriesNotFound");
		await EnsureYearAccessAsync(series.AcademicYearId, operation);
		return series;
	}

	private async Task<AdmissionInquiryLead> GetLeadForTenantAsync(Guid id, string operation)
	{
		var lead = await _leadRepository.GetAsync(id);
		if (lead.TenantId != CurrentTenant.Id) throw new BusinessException("AdmissionCrm:LeadNotFound");
		await _studentScopeAccessService.EnsureCanAccessAdmissionLeadAsync(lead, operation);
		return lead;
	}

	private async Task<AdmissionCrmRegistrationApplication> GetApplicationForTenantAsync(Guid id, string operation)
	{
		var application = await _applicationRepository.GetAsync(id);
		if (application.TenantId != CurrentTenant.Id) throw new BusinessException("AdmissionCrm:RegistrationApplicationNotFound");
		var lead = await _leadRepository.GetAsync(application.LeadId);
		await _studentScopeAccessService.EnsureCanAccessAdmissionLeadAsync(lead, operation);
		return application;
	}

	private AdmissionCrmRegistrationTemplate CreateTemplateEntity(Guid academicYearId, int version, AdmissionCrmRegistrationTemplateUpsertDto input)
	{
		return new AdmissionCrmRegistrationTemplate(
			GuidGenerator.Create(), CurrentTenant.Id, academicYearId, input.Name, version, input.IsFallback,
			Serialize(input.Applicability), Serialize(input.Sections), input.LinkExpiryDays, input.AllowPartialSave,
			input.FeeRequired, input.FeeAmount, input.AutoIssueAtStage, input.Notes);
	}

	private static void ApplyTemplateUpdate(AdmissionCrmRegistrationTemplate template, AdmissionCrmRegistrationTemplateUpsertDto input)
	{
		template.Update(input.Name, input.IsFallback, Serialize(input.Applicability), Serialize(input.Sections), input.LinkExpiryDays,
			input.AllowPartialSave, input.FeeRequired, input.FeeAmount, input.AutoIssueAtStage, input.Notes);
	}

	private static AdmissionCrmRegistrationTemplateUpsertDto ToTemplateUpsert(AdmissionCrmRegistrationTemplate template)
	{
		return new AdmissionCrmRegistrationTemplateUpsertDto
		{
			Name = template.Name,
			IsFallback = template.IsFallback,
			Applicability = Deserialize<AdmissionCrmPlaybookApplicabilityDto>(template.ApplicabilityJson),
			Sections = Deserialize<List<AdmissionCrmRegistrationSectionDto>>(template.SectionsJson),
			LinkExpiryDays = template.LinkExpiryDays,
			AllowPartialSave = template.AllowPartialSave,
			FeeRequired = template.FeeRequired,
			FeeAmount = template.FeeAmount,
			AutoIssueAtStage = template.AutoIssueAtStage,
			Notes = template.Notes
		};
	}

	private static AdmissionCrmRegistrationTemplateDto ToTemplateDto(AdmissionCrmRegistrationTemplate template)
	{
		var input = ToTemplateUpsert(template);
		return new AdmissionCrmRegistrationTemplateDto
		{
			Id = template.Id, AcademicYearId = template.AcademicYearId, Version = template.Version, Status = template.Status,
			PublishedAt = template.PublishedAt, RetiredAt = template.RetiredAt,
			Name = input.Name, IsFallback = input.IsFallback, Applicability = input.Applicability, Sections = input.Sections,
			LinkExpiryDays = input.LinkExpiryDays, AllowPartialSave = input.AllowPartialSave, FeeRequired = input.FeeRequired,
			FeeAmount = input.FeeAmount, AutoIssueAtStage = input.AutoIssueAtStage, Notes = input.Notes
		};
	}

	private static AdmissionCrmFormNumberSeriesDto ToSeriesDto(AdmissionCrmFormNumberSeries series)
	{
		return new AdmissionCrmFormNumberSeriesDto
		{
			Id = series.Id, AcademicYearId = series.AcademicYearId, Name = series.Name, Prefix = series.Prefix, Suffix = series.Suffix,
			Padding = series.Padding, NextNumber = series.NextNumber, Applicability = Deserialize<AdmissionCrmPlaybookApplicabilityDto>(series.ApplicabilityJson),
			IsActive = series.IsActive, NextPreview = series.Prefix + series.NextNumber.ToString($"D{series.Padding}") + series.Suffix
		};
	}

	private static AdmissionCrmRegistrationApplicationDto ToApplicationDto(AdmissionCrmRegistrationApplication application)
	{
		var snapshot = Deserialize<AdmissionCrmRegistrationTemplateDto>(application.TemplateSnapshotJson);
		return new AdmissionCrmRegistrationApplicationDto
		{
			Id = application.Id, ConcurrencyStamp = application.ConcurrencyStamp, AcademicYearId = application.AcademicYearId, LeadId = application.LeadId,
			RegistrationTemplateId = application.RegistrationTemplateId, RegistrationTemplateVersion = application.RegistrationTemplateVersion,
			PlaybookId = application.PlaybookId, FormNumber = application.FormNumber, Status = application.Status, Sections = snapshot.Sections,
			Provenance = application.Provenance, DataEntryState = application.DataEntryState,
			PhysicalFormNumber = application.PhysicalFormNumber, PhysicalFormReceivedDate = application.PhysicalFormReceivedDate,
			PhysicalFormReceivedByUserId = application.PhysicalFormReceivedByUserId,
			ScannedFormAttachmentReference = application.ScannedFormAttachmentReference,
			Prefill = Deserialize<Dictionary<string, JsonElement>>(application.PrefillJson),
			Responses = Deserialize<Dictionary<string, JsonElement>>(application.ResponseJson),
			CorrectionFieldKeys = Deserialize<List<string>>(application.CorrectionJson), CompletionPercentage = application.CompletionPercentage,
			IssuedAt = application.IssuedAt, LinkExpiresAt = application.LinkExpiresAt, OpenedAt = application.OpenedAt,
			LastSavedAt = application.LastSavedAt, SubmittedAt = application.SubmittedAt, VerifiedAt = application.VerifiedAt,
			FeeAmount = application.FeeAmount, FeeStatus = application.FeeStatus, PaymentReference = application.PaymentReference,
			LifecycleReason = application.LifecycleReason
		};
	}

	internal static Dictionary<string, object?> BuildPrefill(AdmissionInquiryLead lead)
	{
		return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
		{
			["firstName"] = lead.FirstName, ["middleName"] = lead.MiddleName, ["lastName"] = lead.LastName,
			["dateOfBirth"] = lead.DateOfBirth?.ToString("yyyy-MM-dd"), ["parentGuardianName"] = lead.ParentGuardianName,
			["primaryContactNo"] = lead.PrimaryContactNo, ["email"] = lead.Email, ["desiredClassName"] = lead.DesiredClassName,
			["campus"] = lead.CampusBranchName,
			["admissionRoute"] = lead.IntendedRouteNameSnapshot ?? lead.IntendedRouteType?.ToString(),
			["admissionRouteDefinitionId"] = lead.IntendedRouteDefinitionId,
			["admissionRouteDefinitionVersion"] = lead.IntendedRouteDefinitionVersion
		};
	}

	private static HashSet<string> GetFieldKeys(AdmissionCrmRegistrationApplication application)
	{
		var snapshot = Deserialize<AdmissionCrmRegistrationTemplateDto>(application.TemplateSnapshotJson);
		return snapshot.Sections.SelectMany(x => x.Fields).Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
	}

	private async Task AddApplicationTimelineAsync(AdmissionCrmRegistrationApplication application, AdmissionCrmTimelineEventType type, string title, string? details)
	{
		var lead = await _leadRepository.GetAsync(application.LeadId);
		await AddTimelineAsync(lead, type, title, details, application.Id);
	}

	private async Task AddTimelineAsync(AdmissionInquiryLead lead, AdmissionCrmTimelineEventType type, string title, string? details, Guid relatedId)
	{
		await _timelineRepository.InsertAsync(new AdmissionCrmTimelineEvent(
			GuidGenerator.Create(), CurrentTenant.Id, lead.Id, lead.AcademicYearId, type, title, details, relatedId,
			CurrentUser.Id, new DateTimeOffset(Clock.Now)), autoSave: true);
	}

	private static bool HasApplicability(AdmissionCrmPlaybookApplicabilityDto value) =>
		new[] { value.Campus, value.DesiredClass, value.Board, value.Medium, value.ApplicantCategory, value.AdmissionRound, value.AdmissionRoute }
			.Any(x => !string.IsNullOrWhiteSpace(x));

	private static bool SameApplicability(AdmissionCrmPlaybookApplicabilityDto left, AdmissionCrmPlaybookApplicabilityDto right) =>
		CodeComparer.Equals(left.Campus, right.Campus) && CodeComparer.Equals(left.DesiredClass, right.DesiredClass) &&
		CodeComparer.Equals(left.Board, right.Board) && CodeComparer.Equals(left.Medium, right.Medium) &&
		CodeComparer.Equals(left.ApplicantCategory, right.ApplicantCategory) && CodeComparer.Equals(left.AdmissionRound, right.AdmissionRound) &&
		CodeComparer.Equals(left.AdmissionRoute, right.AdmissionRoute);

	private static bool MatchesApplicability(AdmissionCrmPlaybookApplicabilityDto configured, AdmissionCrmPlaybookApplicabilityDto actual) =>
		Matches(configured.Campus, actual.Campus) && Matches(configured.DesiredClass, actual.DesiredClass) &&
		Matches(configured.Board, actual.Board) && Matches(configured.Medium, actual.Medium) &&
		Matches(configured.ApplicantCategory, actual.ApplicantCategory) && Matches(configured.AdmissionRound, actual.AdmissionRound) &&
		Matches(configured.AdmissionRoute, actual.AdmissionRoute);

	private static bool Matches(string? configured, string? actual) =>
		string.IsNullOrWhiteSpace(configured) || CodeComparer.Equals(configured.Trim(), actual?.Trim());

	private static int ApplicabilityScore(AdmissionCrmPlaybookApplicabilityDto value) =>
		new[] { value.Campus, value.DesiredClass, value.Board, value.Medium, value.ApplicantCategory, value.AdmissionRound, value.AdmissionRoute }
			.Count(x => !string.IsNullOrWhiteSpace(x));

	private static string CreateRawToken()
	{
		var bytes = RandomNumberGenerator.GetBytes(32);
		return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
	}

	private string EncryptRequired(string value) =>
		_stringEncryptionService.Encrypt(value) ?? throw new BusinessException("AdmissionCrm:EncryptionFailed");

	private static string HashSecret(string value) =>
		Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim()))).ToLowerInvariant();

	private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

	private static T Deserialize<T>(string json) =>
		JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new BusinessException("AdmissionCrm:InvalidStoredRegistrationConfiguration");
}

using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPublicRegistrationWorkflow(
	AdmissionCrmPublicAccess access,
	IRepository<AdmissionCrmRegistrationApplication, Guid> applicationRepository,
	IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository,
	AdmissionCrmDocumentApplicantWorkflow documents) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmRegistrationApplicationDto> SaveAsync(
		Guid tenantId,
		string token,
		SavePublicAdmissionCrmRegistrationDto input)
	{
		using (CurrentTenant.Change(tenantId))
		{
			var (statusAccess, lead) = await access.GetAsync(token);
			await access.EnsureAccessSessionAsync(statusAccess, lead, input.AccessSessionToken);
			var application = await GetApplicationAsync(lead.Id);
			application.EnsureResponseVersion(input.ConcurrencyStamp);
			var template = AdmissionCrmPublicRegistrationMapper.DeserializeTemplate(application.TemplateSnapshotJson);
			if (!template.AllowPartialSave)
			{
				throw new BusinessException("AdmissionCrm:RegistrationPartialSaveDisabled");
			}

			var completion = AdmissionCrmRegistrationResponseValidator.Validate(
				application, template, input.Responses, requireComplete: false);
			application.Save(
				JsonSerializer.Serialize(input.Responses, AdmissionCrmPublicFormat.JsonOptions),
				completion,
				Clock.Now);
			await applicationRepository.UpdateAsync(application, autoSave: true);
			await AddTimelineAsync(
				lead, application.Id, AdmissionCrmTimelineEventType.RegistrationSaved,
				"Registration progress saved", $"Completion: {completion:0.##}%.");
			return AdmissionCrmPublicRegistrationMapper.ToDto(application);
		}
	}

	public async Task<AdmissionCrmRegistrationApplicationDto> SubmitAsync(
		Guid tenantId,
		string token,
		SavePublicAdmissionCrmRegistrationDto input)
	{
		using (CurrentTenant.Change(tenantId))
		{
			var (statusAccess, lead) = await access.GetAsync(token);
			await access.EnsureAccessSessionAsync(statusAccess, lead, input.AccessSessionToken);
			var application = await GetApplicationAsync(lead.Id);
			application.EnsureResponseVersion(input.ConcurrencyStamp);
			var template = AdmissionCrmPublicRegistrationMapper.DeserializeTemplate(application.TemplateSnapshotJson);
			AdmissionCrmRegistrationResponseValidator.Validate(
				application, template, input.Responses, requireComplete: true);
			var isResubmission = application.Status == AdmissionCrmRegistrationStatus.ReturnedForCorrection;
			application.Submit(
				JsonSerializer.Serialize(input.Responses, AdmissionCrmPublicFormat.JsonOptions),
				Clock.Now,
				isResubmission);
			await applicationRepository.UpdateAsync(application, autoSave: true);
			await AddTimelineAsync(
				lead, application.Id, AdmissionCrmTimelineEventType.RegistrationSubmitted,
				isResubmission ? "Registration corrections submitted" : "Registration submitted", null);
			return AdmissionCrmPublicRegistrationMapper.ToDto(application);
		}
	}

	public async Task<AdmissionCrmApplicantDocumentChecklistDto> UploadDocumentAsync(
		Guid tenantId,
		string token,
		string accessSessionToken,
		ProcessedAdmissionCrmApplicantDocumentUploadDto input)
	{
		using (CurrentTenant.Change(tenantId))
		{
			var (statusAccess, lead) = await access.GetAsync(token);
			await access.EnsureAccessSessionAsync(statusAccess, lead, accessSessionToken);
			return await documents.UploadAsync(lead.Id, input);
		}
	}

	public async Task<AdmissionCrmRegistrationApplication?> GetApplicationOrDefaultAsync(Guid leadId)
	{
		var queryable = await applicationRepository.GetQueryableAsync();
		var applications = await AsyncExecuter.ToListAsync(queryable.Where(application =>
			application.TenantId == CurrentTenant.Id && application.LeadId == leadId));
		return applications.OrderByDescending(application => application.IssuedAt)
			.FirstOrDefault(application => application.Status != AdmissionCrmRegistrationStatus.Revoked) ??
			applications.OrderByDescending(application => application.IssuedAt).FirstOrDefault();
	}

	public async Task OpenIfNeededAsync(AdmissionInquiryLead lead, AdmissionCrmRegistrationApplication? application)
	{
		if (application == null || application.OpenedAt != null)
		{
			return;
		}

		application.MarkOpened(Clock.Now);
		await applicationRepository.UpdateAsync(application, autoSave: true);
		await AddTimelineAsync(
			lead, application.Id, AdmissionCrmTimelineEventType.RegistrationOpened, "Registration opened", null);
	}

	public async Task WithdrawIfActiveAsync(AdmissionInquiryLead lead, string reason)
	{
		var application = await GetApplicationOrDefaultAsync(lead.Id);
		if (application == null || application.Status is AdmissionCrmRegistrationStatus.Withdrawn or
			AdmissionCrmRegistrationStatus.Revoked)
		{
			return;
		}

		application.Withdraw($"Withdrawn by family: {reason}");
		await applicationRepository.UpdateAsync(application, autoSave: true);
	}

	private async Task<AdmissionCrmRegistrationApplication> GetApplicationAsync(Guid leadId) =>
		await GetApplicationOrDefaultAsync(leadId) ??
		throw new BusinessException("AdmissionCrm:RegistrationApplicationNotFound");

	private Task AddTimelineAsync(
		AdmissionInquiryLead lead,
		Guid applicationId,
		AdmissionCrmTimelineEventType eventType,
		string title,
		string? details) =>
		timelineRepository.InsertAsync(
			new AdmissionCrmTimelineEvent(
				GuidGenerator.Create(), CurrentTenant.Id, lead.Id, lead.AcademicYearId,
				eventType, title, details, applicationId, actorUserId: null,
				occurredAt: new DateTimeOffset(Clock.Now)),
			autoSave: true);
}

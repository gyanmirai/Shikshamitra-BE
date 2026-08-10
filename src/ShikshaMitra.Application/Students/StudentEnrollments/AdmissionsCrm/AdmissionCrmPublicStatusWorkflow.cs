using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPublicStatusWorkflow(
	AdmissionCrmPublicAccess accessWorkflow,
	AdmissionCrmPublicStatusComposer statusComposer,
	AdmissionCrmPublicFamilyDirectory familyDirectory,
	AdmissionCrmPublicCommunicationPreferences preferences,
	IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionApplicantStatusDto> GetAsync(
		Guid tenantId, string token, string accessSessionToken, string? language)
	{
		using (CurrentTenant.Change(tenantId))
		{
			var (access, lead) = await accessWorkflow.GetAsync(token);
			await accessWorkflow.EnsureAccessSessionAsync(access, lead, accessSessionToken);
			return await BuildVerifiedAsync(lead, access, accessSessionToken, language);
		}
	}

	[UnitOfWork(IsDisabled = true)]
	public async Task<AdmissionApplicantStatusDto> UpdatePreferencesAsync(
		Guid tenantId, string token, UpdatePublicAdmissionCommunicationPreferencesDto input)
	{
		using (CurrentTenant.Change(tenantId))
		using (var uow = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true }, requiresNew: true))
		{
			var (access, lead) = await accessWorkflow.GetAsync(token);
			await accessWorkflow.EnsureAccessSessionAsync(access, lead, input.AccessSessionToken);
			var now = Clock.Now;
			await preferences.SetAsync(lead, AdmissionCrmMessageChannel.WhatsApp, lead.PrimaryContactNo, input.WhatsApp, now);
			await preferences.SetAsync(lead, AdmissionCrmMessageChannel.Sms, lead.PrimaryContactNo, input.Sms, now);
			await preferences.SetAsync(lead, AdmissionCrmMessageChannel.Email, lead.Email, input.Email, now);
			await timelineRepository.InsertAsync(
				new AdmissionCrmTimelineEvent(
					GuidGenerator.Create(), tenantId, lead.Id, lead.AcademicYearId,
					AdmissionCrmTimelineEventType.CommunicationPreferencesChanged,
					"Communication preferences updated",
					"The family updated admissions communication preferences in the secure applicant portal.",
					null, null, now),
				autoSave: true);
			var status = await BuildVerifiedAsync(lead, access, input.AccessSessionToken, lead.PreferredCommunicationLanguage);
			await uow.CompleteAsync();
			return status;
		}
	}

	public async Task<AdmissionApplicantStatusDto> BuildVerifiedAsync(
		AdmissionInquiryLead lead,
		AdmissionApplicantStatusAccess access,
		string accessSessionToken,
		string? language)
	{
		var status = await statusComposer.ComposeAsync(lead, language);
		status.AccessSessionToken = accessSessionToken;
		status.AccessSessionExpiresAt = access.AccessSessionExpiresAt;
		status.FamilyApplications = await familyDirectory.GetSummariesAsync(lead, access, Clock.Now);
		return status;
	}
}

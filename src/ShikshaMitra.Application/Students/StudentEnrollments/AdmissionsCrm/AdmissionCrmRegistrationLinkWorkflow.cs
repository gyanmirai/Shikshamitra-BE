using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmRegistrationLinkWorkflow(
	IRepository<AdmissionCrmRegistrationApplication, Guid> repository,
	AdmissionCrmRegistrationAccess access,
	AdmissionCrmApplicantStatusLinkWorkflow statusLinks,
	AdmissionCrmRegistrationTimeline timeline) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmRegistrationApplicationDto> ExtendAsync(Guid id, ExtendAdmissionCrmRegistrationLinkDto input)
	{
		var application = await access.GetApplicationAsync(id, "ExtendAsync");
		application.Extend(input.NewExpiry, input.Reason, Clock.Now);
		await repository.UpdateAsync(application, autoSave: true);
		await timeline.AddForApplicationAsync(application, AdmissionCrmTimelineEventType.RegistrationLinkExtended, "Registration link extended", input.Reason);
		return AdmissionCrmRegistrationModelMapper.ToApplicationDto(application);
	}

	public async Task<AdmissionCrmRegistrationApplicationDto> RevokeAsync(Guid id, AdmissionCrmRegistrationReasonDto input)
	{
		var application = await access.GetApplicationAsync(id, "RevokeAsync");
		application.Revoke(input.Reason, Clock.Now);
		await repository.UpdateAsync(application, autoSave: true);
		await timeline.AddForApplicationAsync(application, AdmissionCrmTimelineEventType.RegistrationLinkRevoked, "Registration link revoked", input.Reason);
		return AdmissionCrmRegistrationModelMapper.ToApplicationDto(application);
	}

	public async Task<AdmissionCrmRegistrationApplicationDto> ReissueAsync(Guid id, AdmissionCrmRegistrationReissueDto input)
	{
		var application = await access.GetApplicationAsync(id, "ReissueAsync");
		var now = Clock.Now;
		application.Reissue(now.AddDays(input.LinkExpiryDays), input.Reason, now);
		await repository.UpdateAsync(application, autoSave: false);
		var lead = await access.GetLeadAsync(application.LeadId, "ReissueAsync");
		var statusPath = await statusLinks.ReissueAsync(lead, now.AddDays(Math.Max(input.LinkExpiryDays, 90)));
		await timeline.AddAsync(lead, AdmissionCrmTimelineEventType.RegistrationLinkReissued, "Registration link reissued", input.Reason, application.Id);
		await CurrentUnitOfWork!.SaveChangesAsync();
		var dto = AdmissionCrmRegistrationModelMapper.ToApplicationDto(application);
		dto.ApplicantStatusPath = statusPath;
		return dto;
	}
}

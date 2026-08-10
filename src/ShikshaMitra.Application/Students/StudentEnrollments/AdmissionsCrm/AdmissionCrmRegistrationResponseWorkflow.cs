using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmRegistrationResponseWorkflow(
	IRepository<AdmissionCrmRegistrationApplication, Guid> repository,
	AdmissionCrmRegistrationAccess access,
	AdmissionCrmRegistrationTimeline timeline) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmRegistrationApplicationDto> SaveAsync(Guid id, SaveAdmissionCrmRegistrationResponsesDto input)
	{
		var application = await access.GetApplicationAsync(id, "SaveStaffResponsesAsync");
		application.EnsureResponseVersion(input.ConcurrencyStamp);
		var template = AdmissionCrmRegistrationResponseValidator.DeserializeTemplate(application.TemplateSnapshotJson);
		if (!template.AllowPartialSave) throw new BusinessException("AdmissionCrm:RegistrationPartialSaveDisabled");
		var completion = AdmissionCrmRegistrationResponseValidator.Validate(application, template, input.Responses, false, true);
		application.Save(AdmissionCrmRegistrationModelMapper.Serialize(input.Responses), completion, Clock.Now);
		await repository.UpdateAsync(application, autoSave: true);
		await timeline.AddForApplicationAsync(application, AdmissionCrmTimelineEventType.RegistrationSaved,
			"Offline registration saved by staff", $"Completion: {completion:0.##}%.");
		return AdmissionCrmRegistrationModelMapper.ToApplicationDto(application);
	}

	public async Task<AdmissionCrmRegistrationApplicationDto> SubmitAsync(Guid id, SaveAdmissionCrmRegistrationResponsesDto input)
	{
		var application = await access.GetApplicationAsync(id, "SubmitStaffResponsesAsync");
		application.EnsureResponseVersion(input.ConcurrencyStamp);
		var template = AdmissionCrmRegistrationResponseValidator.DeserializeTemplate(application.TemplateSnapshotJson);
		AdmissionCrmRegistrationResponseValidator.Validate(application, template, input.Responses, true, true);
		var resubmission = application.Status == AdmissionCrmRegistrationStatus.ReturnedForCorrection;
		application.Submit(AdmissionCrmRegistrationModelMapper.Serialize(input.Responses), Clock.Now, resubmission);
		await repository.UpdateAsync(application, autoSave: true);
		await timeline.AddForApplicationAsync(application, AdmissionCrmTimelineEventType.RegistrationSubmitted,
			resubmission ? "Offline registration corrections submitted by staff" : "Offline registration submitted by staff",
			"Entered from a paper form by an authorized staff member.");
		return AdmissionCrmRegistrationModelMapper.ToApplicationDto(application);
	}

	public async Task<AdmissionCrmRegistrationApplicationDto> ReturnAsync(Guid id, ReturnAdmissionCrmRegistrationForCorrectionDto input)
	{
		var application = await access.GetApplicationAsync(id, "ReturnForCorrectionAsync");
		var validKeys = AdmissionCrmRegistrationModelMapper.GetFieldKeys(application);
		var keys = input.FieldKeys.Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		if (keys.Count == 0 || keys.Any(x => !validKeys.Contains(x)))
			throw new BusinessException("AdmissionCrm:InvalidRegistrationCorrectionFields");
		application.ReturnForCorrection(AdmissionCrmRegistrationModelMapper.Serialize(keys), input.Reason);
		await repository.UpdateAsync(application, autoSave: true);
		await timeline.AddForApplicationAsync(application, AdmissionCrmTimelineEventType.RegistrationReturnedForCorrection,
			"Registration returned for correction", input.Reason);
		return AdmissionCrmRegistrationModelMapper.ToApplicationDto(application);
	}

	public async Task<AdmissionCrmRegistrationApplicationDto> VerifyAsync(Guid id)
	{
		var application = await access.GetApplicationAsync(id, "VerifyAsync");
		application.Verify(Clock.Now);
		await repository.UpdateAsync(application, autoSave: true);
		await timeline.AddForApplicationAsync(application, AdmissionCrmTimelineEventType.RegistrationVerified, "Registration verified", null);
		return AdmissionCrmRegistrationModelMapper.ToApplicationDto(application);
	}
}

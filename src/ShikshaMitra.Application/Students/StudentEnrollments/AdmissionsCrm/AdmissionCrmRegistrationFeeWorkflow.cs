using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmRegistrationFeeWorkflow(
	IRepository<AdmissionCrmRegistrationApplication, Guid> repository,
	AdmissionCrmRegistrationAccess access,
	AdmissionCrmRegistrationTimeline timeline) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmRegistrationApplicationDto> RecordReferenceAsync(Guid id, RecordAdmissionCrmRegistrationPaymentReferenceDto input)
	{
		var application = await access.GetApplicationAsync(id, "RecordPaymentReferenceAsync");
		application.RecordPaymentReference(input.PaymentReference);
		await repository.UpdateAsync(application, autoSave: true);
		await timeline.AddForApplicationAsync(application, AdmissionCrmTimelineEventType.RegistrationPaymentReferenceRecorded,
			"Registration payment reference recorded", input.PaymentReference);
		return AdmissionCrmRegistrationModelMapper.ToApplicationDto(application);
	}

	public async Task<AdmissionCrmRegistrationApplicationDto> WaiveAsync(Guid id, AdmissionCrmRegistrationReasonDto input)
	{
		var application = await access.GetApplicationAsync(id, "WaiveFeeAsync");
		application.WaiveFee(input.Reason);
		await repository.UpdateAsync(application, autoSave: true);
		await timeline.AddForApplicationAsync(application, AdmissionCrmTimelineEventType.RegistrationFeeWaived, "Registration fee waived", input.Reason);
		return AdmissionCrmRegistrationModelMapper.ToApplicationDto(application);
	}
}

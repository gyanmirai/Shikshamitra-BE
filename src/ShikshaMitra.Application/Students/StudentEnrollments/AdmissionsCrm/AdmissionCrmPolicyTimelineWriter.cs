using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPolicyTimelineWriter(
	IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository) : ApplicationService, ITransientDependency
{
	public async Task AddAsync(
		AdmissionInquiryLead lead,
		AdmissionCrmTimelineEventType type,
		string title,
		string details,
		Guid relatedId) => await timelineRepository.InsertAsync(
		new AdmissionCrmTimelineEvent(
			GuidGenerator.Create(), CurrentTenant.Id, lead.Id, lead.AcademicYearId,
			type, title, details, relatedId, CurrentUser.Id, Clock.Now),
		autoSave: true);
}

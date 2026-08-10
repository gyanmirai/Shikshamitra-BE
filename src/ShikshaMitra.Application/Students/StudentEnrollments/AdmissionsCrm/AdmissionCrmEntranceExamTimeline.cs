using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmEntranceExamTimeline(IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository)
	: ApplicationService, ITransientDependency
{
	public Task AddAsync(
		AdmissionInquiryLead lead,
		AdmissionCrmTimelineEventType eventType,
		string title,
		string? details,
		Guid? relatedEntityId) =>
		timelineRepository.InsertAsync(
			new AdmissionCrmTimelineEvent(
				GuidGenerator.Create(), CurrentTenant.Id, lead.Id, lead.AcademicYearId,
				eventType, title, details, relatedEntityId, CurrentUser.Id,
				new DateTimeOffset(Clock.Now)),
			autoSave: false);
}

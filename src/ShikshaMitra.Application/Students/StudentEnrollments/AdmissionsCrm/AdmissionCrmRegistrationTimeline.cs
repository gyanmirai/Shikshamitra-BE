using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmRegistrationTimeline(
	IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository,
	IRepository<AdmissionInquiryLead, Guid> leadRepository) : ApplicationService, ITransientDependency
{
	public async Task AddForApplicationAsync(AdmissionCrmRegistrationApplication application, AdmissionCrmTimelineEventType type, string title, string? details) =>
		await AddAsync(await leadRepository.GetAsync(application.LeadId), type, title, details, application.Id);

	public async Task AddAsync(AdmissionInquiryLead lead, AdmissionCrmTimelineEventType type, string title, string? details, Guid relatedId) =>
		await timelineRepository.InsertAsync(new AdmissionCrmTimelineEvent(
			GuidGenerator.Create(), CurrentTenant.Id, lead.Id, lead.AcademicYearId, type, title, details, relatedId,
			CurrentUser.Id, new DateTimeOffset(Clock.Now)), autoSave: true);
}

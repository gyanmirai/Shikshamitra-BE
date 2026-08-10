using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmDocumentTimelineWriter(IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository)
	: ApplicationService, ITransientDependency
{
	public Task AddAsync(
		Guid leadId,
		Guid yearId,
		AdmissionCrmTimelineEventType type,
		string title,
		string? details,
		Guid relatedId,
		Guid? actorUserId = default) =>
		timelineRepository.InsertAsync(new AdmissionCrmTimelineEvent(
			GuidGenerator.Create(), CurrentTenant.Id, leadId, yearId, type, title, details, relatedId,
			actorUserId == default ? CurrentUser.Id : actorUserId, new DateTimeOffset(Clock.Now)), autoSave: true);
}

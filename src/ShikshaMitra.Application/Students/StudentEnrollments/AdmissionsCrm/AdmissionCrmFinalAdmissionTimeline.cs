using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmFinalAdmissionTimeline(
	IRepository<AdmissionCrmTimelineEvent, Guid> repository,
	AdmissionCrmFinalAdmissionAccess access) : ApplicationService, ITransientDependency
{
	public async Task AddForOfferAsync(AdmissionCrmSeatOffer offer, AdmissionCrmTimelineEventType type, string title, string details) =>
		await AddAsync(await access.GetLeadAsync(offer.LeadId, "AddTimelineForOfferAsync"), type, title, details, offer.Id);

	public async Task AddAsync(AdmissionInquiryLead lead, AdmissionCrmTimelineEventType type, string title, string? details, Guid relatedId) =>
		await repository.InsertAsync(new AdmissionCrmTimelineEvent(GuidGenerator.Create(), CurrentTenant.Id, lead.Id,
			lead.AcademicYearId, type, title, details, relatedId, CurrentUser.Id, Clock.Now), autoSave: true);
}

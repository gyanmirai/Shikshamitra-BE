using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmWaitlistHistoryWorkflow(
	IRepository<AdmissionCrmWaitlistHistory, Guid> repository) : ApplicationService, ITransientDependency
{
	public Task AddAsync(AdmissionCrmWaitlistEntry entry, AdmissionCrmWaitlistHistoryAction action, string reason,
		Guid? offerId = null, int? previousPriorityScore = null) => repository.InsertAsync(new AdmissionCrmWaitlistHistory(
			GuidGenerator.Create(), CurrentTenant.Id, entry.AcademicYearId, entry.Id, entry.LeadId, entry.SeatPoolId,
			action, previousPriorityScore, entry.PriorityScore, reason, offerId, CurrentUser.Id, Clock.Now), autoSave: true);
}

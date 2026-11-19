using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionDashboardQueryService(IRepository<AdmissionApplicationStageInstance, Guid> stages,
	IRepository<AdmissionApplicationWorkflowInstance, Guid> workflows, IRepository<StudentOnboardingCase, Guid> cases)
	: ApplicationService, ITransientDependency
{
	public async Task<IQueryable<AdmissionApplication>> ApplyAsync(IQueryable<AdmissionApplication> query, bool activeOnly, bool pendingDecisionOnly, Guid? divisionId)
	{
		if (activeOnly) query = query.Where(a => a.Status == AdmissionApplicationStatus.Draft || a.Status == AdmissionApplicationStatus.Submitted ||
			a.Status == AdmissionApplicationStatus.InReview || a.Status == AdmissionApplicationStatus.Waitlisted || a.Status == AdmissionApplicationStatus.Approved ||
			a.Status == AdmissionApplicationStatus.Offered || a.Status == AdmissionApplicationStatus.OfferAccepted);
		if (divisionId.HasValue)
		{
			var onboarding = await cases.GetQueryableAsync();
			query = query.Where(a => onboarding.Any(c => c.TenantId == CurrentTenant.Id && c.ApplicationId == a.Id && c.DivisionId == divisionId));
		}
		if (pendingDecisionOnly)
		{
			var stageQuery = await stages.GetQueryableAsync(); var workflowQuery = await workflows.GetQueryableAsync();
			query = query.Where(a => (a.Status == AdmissionApplicationStatus.Submitted || a.Status == AdmissionApplicationStatus.InReview || a.Status == AdmissionApplicationStatus.Waitlisted) &&
				stageQuery.Any(s => s.TenantId == CurrentTenant.Id && s.Id == a.CurrentStageInstanceId && s.Status == AdmissionRuntimeStageStatus.Active &&
					s.StageType == AdmissionWorkflowStageType.Approval && workflowQuery.Any(w => w.TenantId == CurrentTenant.Id && w.Id == s.WorkflowInstanceId &&
						w.ApplicationId == a.Id && w.Status == AdmissionWorkflowInstanceStatus.Active)));
		}
		return query;
	}
}

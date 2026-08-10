using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmGovernanceAutomationCoordinator(AdmissionCrmReportScheduleWorkflow workflow) : ITransientDependency
{
	public Task<AdmissionCrmReportScheduleRunResultDto> RunDueReportSchedulesAsync(Guid academicYearId) =>
		workflow.RunDueAsync(academicYearId, false);
}

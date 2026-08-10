using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmLeadOperationsCoordinator(
	AdmissionCrmLeadAssignmentWorkflow assignment,
	AdmissionCrmLeadLifecycleWorkflow lifecycle,
	AdmissionCrmLeadWorkWorkflow work) : ITransientDependency
{
	public Task<AdmissionCrmLeadDto> AssignOwnerAsync(Guid leadId, AssignAdmissionCrmLeadOwnerDto input) => assignment.AssignOwnerAsync(leadId, input);
	public Task<AdmissionCrmFollowUpTaskDto> CreateTaskAsync(Guid leadId, CreateAdmissionCrmFollowUpTaskDto input) => work.CreateTaskAsync(leadId, input);
	public Task<AdmissionCrmLeadDto> ChangeStageAsync(Guid leadId, ChangeAdmissionCrmLeadStageDto input) => assignment.ChangeStageAsync(leadId, input);
	public Task<AdmissionCrmLeadDto> ArchiveAsync(Guid leadId, ArchiveAdmissionCrmLeadDto input) => lifecycle.ArchiveAsync(leadId, input);
}
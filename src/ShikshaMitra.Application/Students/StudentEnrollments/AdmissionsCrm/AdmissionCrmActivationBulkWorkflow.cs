using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmActivationBulkWorkflow(
	AdmissionCrmActivationLifecycleWorkflow lifecycle,
	AdmissionCrmActivationExecutionWorkflow execution) : ITransientDependency
{
	public Task<AdmissionCrmActivationPreviewDto> PreviewAsync(Guid leadId, PreviewAdmissionCrmActivationDto input) =>
		lifecycle.PreviewAsync(leadId, input);
	public Task<AdmissionCrmActivationDto> ExecuteAsync(Guid activationId) => execution.ExecuteAsync(activationId);
}

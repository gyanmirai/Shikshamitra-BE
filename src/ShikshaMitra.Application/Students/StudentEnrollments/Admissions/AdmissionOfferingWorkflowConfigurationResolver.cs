using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionOfferingWorkflowConfigurationResolver(
	IRepository<AdmissionWorkflowDefinition, Guid> workflows,
	IRepository<AdmissionWorkflowStageDefinition, Guid> stages) : ITransientDependency
{
	public async Task ResolveAsync(AdmissionOfferingWriteDto input, AdmissionOffering? existing = null, bool preserveRouting = true)
	{
		input.Track = preserveRouting ? existing?.Track ?? AdmissionTrack.General : AdmissionTrack.General;
		input.EntryMode = preserveRouting ? existing?.EntryMode ?? AdmissionEntryMode.Inquiry : AdmissionEntryMode.Inquiry;
		var workflow = input.WorkflowDefinitionId.HasValue
			? await workflows.FindAsync(input.WorkflowDefinitionId.Value) : null;
		var configuredForms = workflow == null ? [] : (await stages.GetListAsync(stage =>
			stage.WorkflowDefinitionId == workflow.Id && stage.StageType == AdmissionWorkflowStageType.Form))
			.Select(stage => AdmissionFormStageAccessService.ReadFormDefinitionId(stage.ConfigurationJson))
			.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
		// Published workflow choices are authoritative. Legacy stored references remain
		// usable only when editing the same workflow without an explicit form choice.
		var retainLegacy = existing?.WorkflowDefinitionId == input.WorkflowDefinitionId;
		input.InquiryFormDefinitionId ??= workflow?.DefaultInquiryFormDefinitionId ??
			(retainLegacy ? existing?.InquiryFormDefinitionId : null);
		input.ApplicationFormDefinitionId = configuredForms.Length == 1 ? configuredForms[0] :
			configuredForms.Length == 0 && retainLegacy ? existing?.ApplicationFormDefinitionId : null;
	}
}

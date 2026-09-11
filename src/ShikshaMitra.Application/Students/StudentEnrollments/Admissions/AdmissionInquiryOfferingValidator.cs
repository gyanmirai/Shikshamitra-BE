using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionInquiryOfferingValidator(
	IRepository<AdmissionWorkflowStageDefinition, Guid> stages,
	AdmissionInquiryPeopleService people) : ITransientDependency
{
	public async Task<List<AdmissionOfferingValidationError>> ValidateAsync(AdmissionCycle cycle, AdmissionOfferingWriteDto input)
	{
		if (!input.WorkflowDefinitionId.HasValue || input.BoardId == Guid.Empty || input.MediumId == Guid.Empty ||
			input.StageId == Guid.Empty || input.StandardId == Guid.Empty) return [];
		var definitions = await stages.GetListAsync(stage => stage.WorkflowDefinitionId == input.WorkflowDefinitionId.Value &&
			stage.StageType == AdmissionWorkflowStageType.Inquiry);
		foreach (var policy in definitions.Select(AdmissionInquiryPolicyResolver.Read).Where(policy => policy != null))
		{
			var counsellors = policy!.CounsellorPoolUserIds.ToList();
			if (policy.DefaultCounsellorId.HasValue) counsellors.Add(policy.DefaultCounsellorId.Value);
			foreach (var counsellorId in counsellors.Distinct())
				if (!await people.CanAccessHierarchyAsync(counsellorId, cycle, input))
					return [new(AdmissionOfferingValidationCodes.InquiryCounsellorUnavailable,
						"Choose active counsellors with access to this offering, or remove them from the Inquiry stage.")];
		}
		return [];
	}
}

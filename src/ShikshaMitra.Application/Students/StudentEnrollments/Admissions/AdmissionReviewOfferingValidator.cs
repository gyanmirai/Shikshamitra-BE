using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionReviewOfferingValidator(IRepository<AdmissionWorkflowStageDefinition, Guid> stages,
	AdmissionReviewPeopleService people) : ITransientDependency
{
	public async Task<List<AdmissionOfferingValidationError>> ValidateAsync(AdmissionCycle cycle, AdmissionOfferingWriteDto input)
	{
		if (!input.WorkflowDefinitionId.HasValue || input.BoardId == Guid.Empty || input.MediumId == Guid.Empty ||
			input.StageId == Guid.Empty || input.StandardId == Guid.Empty) return [];
		var definitions = await stages.GetListAsync(stage => stage.WorkflowDefinitionId == input.WorkflowDefinitionId.Value && stage.StageType == AdmissionWorkflowStageType.Review);
		foreach (var stage in definitions)
		{
			var policy = AdmissionReviewAccessService.ReadPolicy(stage);
			if (policy?.AssignedReviewerId is Guid reviewerId && !await people.CanAssignHierarchyAsync(reviewerId, policy.ReviewerRole, cycle, input))
				return [new(AdmissionOfferingValidationCodes.ReviewAssigneeUnavailable,
					"Choose an active default reviewer with the configured role and access to this offering, or remove the default assignment.")];
		}
		return [];
	}
}

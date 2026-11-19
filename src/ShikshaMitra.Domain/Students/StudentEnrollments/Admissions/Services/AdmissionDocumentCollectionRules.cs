using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public static class AdmissionDocumentCollectionRules
{
	public static bool IsLinkedForm(AdmissionWorkflowStageDefinition? form, AdmissionWorkflowStageDefinition documents,
		AdmissionDocumentPolicy policy, IReadOnlyCollection<AdmissionWorkflowTransitionDefinition> transitions)
	{
		if (form == null || form.StageType != AdmissionWorkflowStageType.Form || form.WorkflowDefinitionId != documents.WorkflowDefinitionId ||
			!string.Equals(form.Code, policy.CollectionFormStageCode, StringComparison.Ordinal)) return false;
		var pending = new Queue<Guid>();
		var visited = new HashSet<Guid>();
		pending.Enqueue(form.Id);
		while (pending.TryDequeue(out var id))
		{
			if (!visited.Add(id)) continue;
			foreach (var route in transitions.Where(item => item.WorkflowDefinitionId == documents.WorkflowDefinitionId && item.FromStageId == id))
			{
				if (route.ToStageId == documents.Id) return true;
				pending.Enqueue(route.ToStageId);
			}
		}
		return false;
	}
}

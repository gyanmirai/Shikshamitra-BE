using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionWorkflowTransitionSelector(
	AdmissionWorkflowConditionEvaluator conditionEvaluator) : DomainService
{
	public AdmissionWorkflowTransitionDefinition Select(
		IReadOnlyCollection<AdmissionWorkflowTransitionDefinition> transitions,
		IReadOnlyDictionary<string, string> values,
		Guid? requestedTransitionId = null,
		bool adminOverride = false)
	{
		if (requestedTransitionId.HasValue)
		{
			var requested = transitions.SingleOrDefault(transition => transition.Id == requestedTransitionId.Value);
			if (requested != null &&
				(adminOverride || conditionEvaluator.Evaluate(requested.ConditionRuleJson, values)))
			{
				return requested;
			}

			throw new BusinessException(AdmissionApplicationErrorCodes.NoEligibleTransition);
		}

		var eligible = transitions
			.Where(transition => !transition.IsDefault)
			.Where(transition => conditionEvaluator.Evaluate(transition.ConditionRuleJson, values))
			.OrderBy(transition => transition.Priority)
			.ToList();
		if (eligible.Count > 0)
		{
			var winningPriority = eligible[0].Priority;
			if (eligible.Count(transition => transition.Priority == winningPriority) > 1)
			{
				throw new BusinessException(AdmissionApplicationErrorCodes.AmbiguousTransition);
			}

			return eligible[0];
		}

		return transitions
			.Where(transition => transition.IsDefault)
			.OrderBy(transition => transition.Priority)
			.FirstOrDefault() ??
			throw new BusinessException(AdmissionApplicationErrorCodes.NoEligibleTransition);
	}
}

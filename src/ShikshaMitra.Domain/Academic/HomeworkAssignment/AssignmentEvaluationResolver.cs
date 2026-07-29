using Volo.Abp;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public readonly record struct EffectiveAssignmentEvaluation(
	bool IsRequired,
	EvaluationMethod Method);

public static class AssignmentEvaluationResolver
{
	public static EffectiveAssignmentEvaluation Resolve(
		AssignmentEntity assignment,
		AssignmentType? assignmentType = null)
	{
		var evaluationRequired = assignment.EvaluationRequired ?? assignmentType?.EvaluationRequired ?? true;
		var evaluationMethod = assignment.EvaluationMethod ?? assignmentType?.DefaultEvaluationMethod;

		if (!evaluationRequired)
		{
			if (assignment.TotalMarks > 0)
			{
				throw new BusinessException(HomeworkAssignmentErrorCodes.IncompatibleEvaluationMarks)
					.WithData("AssignmentId", assignment.Id);
			}

			return new EffectiveAssignmentEvaluation(false, EvaluationMethod.NoEvaluation);
		}

		// Compatibility for legacy rows and pre-alignment assignment types. Existing marks imply
		// marks-based evaluation; otherwise feedback-only is the only lossless persisted fallback.
		evaluationMethod ??= assignment.TotalMarks > 0
			? EvaluationMethod.Marks
			: EvaluationMethod.FeedbackOnly;

		if (evaluationMethod == EvaluationMethod.NoEvaluation)
		{
			throw new BusinessException(HomeworkAssignmentErrorCodes.EvaluationMethodRequired)
				.WithData("AssignmentId", assignment.Id);
		}

		if (evaluationMethod is EvaluationMethod.Grade or EvaluationMethod.Checklist)
		{
			throw new BusinessException(HomeworkAssignmentErrorCodes.UnsupportedEvaluationPersistence)
				.WithData("AssignmentId", assignment.Id)
				.WithData("EvaluationMethod", evaluationMethod);
		}

		if (evaluationMethod == EvaluationMethod.Marks)
		{
			if (assignment.TotalMarks <= 0)
			{
				throw new BusinessException(HomeworkAssignmentErrorCodes.InvalidDefaultMarks)
					.WithData("AssignmentId", assignment.Id);
			}
		}
		else if (assignment.TotalMarks > 0)
		{
			throw new BusinessException(HomeworkAssignmentErrorCodes.IncompatibleEvaluationMarks)
				.WithData("AssignmentId", assignment.Id)
				.WithData("EvaluationMethod", evaluationMethod);
		}

		return new EffectiveAssignmentEvaluation(true, evaluationMethod.Value);
	}
}

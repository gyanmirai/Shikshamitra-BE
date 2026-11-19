using FluentValidation;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class CreateAssignmentTypeDtoValidator : AbstractValidator<CreateAssignmentTypeDto>
{
	public CreateAssignmentTypeDtoValidator()
	{
		Include(new AssignmentTypeSettingsValidator<CreateAssignmentTypeDto>());
	}
}

public class UpdateAssignmentTypeDtoValidator : AbstractValidator<UpdateAssignmentTypeDto>
{
	public UpdateAssignmentTypeDtoValidator()
	{
		Include(new AssignmentTypeSettingsValidator<UpdateAssignmentTypeDto>());
	}
}

internal class AssignmentTypeSettingsValidator<TDto> : AbstractValidator<TDto>
	where TDto : class
{
	public AssignmentTypeSettingsValidator()
	{
		RuleFor(x => GetName(x))
			.Must(name =>
			{
				var normalizedName = name?.Trim();
				return normalizedName is { Length: >= AssignmentTypeConsts.MinNameLength and <= AssignmentTypeConsts.MaxNameLength };
			})
			.WithMessage($"Name must be between {AssignmentTypeConsts.MinNameLength} and {AssignmentTypeConsts.MaxNameLength} characters.");

		RuleFor(x => GetSubmissionType(x))
			.IsInEnum()
			.When(x => GetSubmissionType(x).HasValue);

		RuleFor(x => GetDueDays(x))
			.GreaterThanOrEqualTo(1)
			.When(x => GetDueDays(x).HasValue);

		RuleFor(x => GetEvaluationMethod(x))
			.IsInEnum()
			.When(x => GetEvaluationMethod(x).HasValue);

		RuleFor(x => GetStatus(x)).IsInEnum();

		RuleFor(x => GetEvaluationMethod(x))
			.NotNull()
			.NotEqual(EvaluationMethod.NoEvaluation)
			.When(GetEvaluationRequired)
			.WithMessage("An evaluation method other than NoEvaluation is required when evaluation is enabled.");

		RuleFor(x => GetMarks(x))
			.GreaterThanOrEqualTo(0)
			.When(x => GetEvaluationRequired(x) &&
				GetEvaluationMethod(x) == EvaluationMethod.Marks &&
				GetMarks(x).HasValue)
			.WithMessage("Default marks cannot be negative when supplied.");

		RuleFor(x => GetMarks(x))
			.Null()
			.When(x => !GetEvaluationRequired(x) || GetEvaluationMethod(x) != EvaluationMethod.Marks)
			.WithMessage("Default marks are allowed only when evaluation is enabled and uses marks.");
	}

	private static string GetName(TDto input) => input switch
	{
		CreateAssignmentTypeDto create => create.AssignmentTypeName,
		UpdateAssignmentTypeDto update => update.AssignmentTypeName,
		_ => string.Empty
	};

	private static SubmissionType? GetSubmissionType(TDto input) => input switch
	{
		CreateAssignmentTypeDto create => create.DefaultSubmissionType,
		UpdateAssignmentTypeDto update => update.DefaultSubmissionType,
		_ => null
	};

	private static decimal? GetMarks(TDto input) => input switch
	{
		CreateAssignmentTypeDto create => create.DefaultMarks,
		UpdateAssignmentTypeDto update => update.DefaultMarks,
		_ => null
	};

	private static int? GetDueDays(TDto input) => input switch
	{
		CreateAssignmentTypeDto create => create.DefaultDueDays,
		UpdateAssignmentTypeDto update => update.DefaultDueDays,
		_ => null
	};

	private static bool GetEvaluationRequired(TDto input) => input switch
	{
		CreateAssignmentTypeDto create => create.EvaluationRequired,
		UpdateAssignmentTypeDto update => update.EvaluationRequired,
		_ => false
	};

	private static EvaluationMethod? GetEvaluationMethod(TDto input) => input switch
	{
		CreateAssignmentTypeDto create => create.DefaultEvaluationMethod,
		UpdateAssignmentTypeDto update => update.DefaultEvaluationMethod,
		_ => null
	};

	private static TaskTypeStatus GetStatus(TDto input) => input switch
	{
		CreateAssignmentTypeDto create => create.Status,
		UpdateAssignmentTypeDto update => update.Status,
		_ => default
	};
}

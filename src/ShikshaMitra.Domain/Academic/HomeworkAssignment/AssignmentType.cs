using System.Diagnostics.CodeAnalysis;
using System;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// Represents an assignment type (e.g., Group Project, Case Study, Research Paper)
/// associated with an academic medium
/// </summary>
public class AssignmentType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	/// <summary>
	/// Gets the tenant ID for multi-tenancy support
	/// </summary>
	public Guid? TenantId { get; private set; }

	/// <summary>
	/// Gets the name of the assignment type
	/// </summary>
	public string Name { get; private set; }

	/// <summary>
	/// Gets the associated academic medium ID
	/// </summary>
	public Guid AcademicMediumId { get; private set; }

	/// <summary>
	/// Navigation property to EducationMedium
	/// </summary>
	public virtual EducationMedium AcademicMedium { get; } = null!; // Populated by EF relationship materialization.

	/// <summary>
	/// Gets the description of the assignment type
	/// </summary>
	public string? Description { get; private set; }

	/// <summary>
	/// Gets the default submission type
	/// </summary>
	public SubmissionType? DefaultSubmissionType { get; private set; }

	/// <summary>
	/// Gets the default marks
	/// </summary>
	public decimal? DefaultMarks { get; private set; }

	/// <summary>
	/// Gets the default due days
	/// </summary>
	public int? DefaultDueDays { get; private set; }

	/// <summary>
	/// Gets whether evaluation is required by default
	/// </summary>
	public bool EvaluationRequired { get; private set; } = true;

	/// <summary>
	/// Gets the default evaluation method
	/// </summary>
	public EvaluationMethod? DefaultEvaluationMethod { get; private set; }

	/// <summary>
	/// Gets the status of the assignment type
	/// </summary>
	public TaskTypeStatus Status { get; private set; } = TaskTypeStatus.Active;

	/// <summary>
	/// Private constructor for EF Core
	/// </summary>
	private AssignmentType()
	{
		// EF materializes these required columns after this ORM constructor.
		Name = null!;

		/* This constructor is for deserialization / ORM purpose */
	}

	/// <summary>
	/// Internal constructor for creating assignment type through domain manager
	/// </summary>
	internal AssignmentType(
		Guid id,
		string name,
		Guid academicMediumId,
		Guid? tenantId = null,
		string? description = null,
		SubmissionType? defaultSubmissionType = null,
		decimal? defaultMarks = null,
		int? defaultDueDays = null,
		bool evaluationRequired = true,
		EvaluationMethod? defaultEvaluationMethod = null,
		TaskTypeStatus status = TaskTypeStatus.Active
	)
		: base(id)
	{
		TenantId = tenantId;
		AcademicMediumId = Check.NotNull(academicMediumId, nameof(academicMediumId));
		SetName(name);
		SetDescription(description);
		SetDefaultSubmissionType(defaultSubmissionType);
		SetDefaultDueDays(defaultDueDays);
		SetEvaluationSettings(evaluationRequired, defaultEvaluationMethod, defaultMarks);
		SetStatus(status);
	}

	/// <summary>
	/// Sets the name with validation
	/// </summary>
	[MemberNotNull(nameof(Name))]
	internal AssignmentType SetName(string name)
	{
		var normalizedName = name?.Trim();
		if (string.IsNullOrWhiteSpace(normalizedName) ||
			normalizedName.Length < AssignmentTypeConsts.MinNameLength ||
			normalizedName.Length > AssignmentTypeConsts.MaxNameLength)
		{
			throw new BusinessException(HomeworkAssignmentErrorCodes.InvalidAssignmentTypeName)
				.WithData("MinLength", AssignmentTypeConsts.MinNameLength)
				.WithData("MaxLength", AssignmentTypeConsts.MaxNameLength);
		}

		Name = normalizedName;
		return this;
	}

	internal AssignmentType SetDescription(string? description)
	{
		var normalizedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
		if (normalizedDescription?.Length > 250)
		{
			throw new ArgumentException("Description cannot exceed 250 characters.", nameof(description));
		}
		Description = normalizedDescription;
		return this;
	}

	internal AssignmentType SetDefaultSubmissionType(SubmissionType? defaultSubmissionType)
	{
		if (defaultSubmissionType.HasValue && !Enum.IsDefined(defaultSubmissionType.Value))
		{
			throw new BusinessException(HomeworkAssignmentErrorCodes.InvalidSubmissionType)
				.WithData("SubmissionType", defaultSubmissionType.Value);
		}

		DefaultSubmissionType = defaultSubmissionType;
		return this;
	}

	internal AssignmentType SetDefaultDueDays(int? defaultDueDays)
	{
		if (defaultDueDays.HasValue && defaultDueDays.Value < 1)
		{
			throw new BusinessException(HomeworkAssignmentErrorCodes.InvalidDefaultDueDays);
		}
		DefaultDueDays = defaultDueDays;
		return this;
	}

	internal AssignmentType SetEvaluationSettings(
		bool evaluationRequired,
		EvaluationMethod? defaultEvaluationMethod,
		decimal? defaultMarks)
	{
		if (defaultEvaluationMethod.HasValue && !Enum.IsDefined(defaultEvaluationMethod.Value))
		{
			throw new BusinessException(HomeworkAssignmentErrorCodes.InvalidEvaluationMethod)
				.WithData("EvaluationMethod", defaultEvaluationMethod.Value);
		}

		if (!evaluationRequired)
		{
			EvaluationRequired = false;
			DefaultEvaluationMethod = EvaluationMethod.NoEvaluation;
			DefaultMarks = null;
			return this;
		}

		if (!defaultEvaluationMethod.HasValue || defaultEvaluationMethod == EvaluationMethod.NoEvaluation)
		{
			throw new BusinessException(HomeworkAssignmentErrorCodes.EvaluationMethodRequired);
		}

		if (defaultMarks.HasValue && defaultMarks.Value < 0)
		{
			throw new BusinessException(HomeworkAssignmentErrorCodes.InvalidDefaultMarks);
		}

		if (defaultEvaluationMethod != EvaluationMethod.Marks && defaultMarks.HasValue)
		{
			throw new BusinessException(HomeworkAssignmentErrorCodes.InvalidDefaultMarks);
		}

		EvaluationRequired = true;
		DefaultEvaluationMethod = defaultEvaluationMethod;
		DefaultMarks = defaultMarks;
		return this;
	}

	internal AssignmentType SetStatus(TaskTypeStatus status)
	{
		if (!Enum.IsDefined(status))
		{
			throw new BusinessException(HomeworkAssignmentErrorCodes.InvalidTaskTypeStatus)
				.WithData("Status", status);
		}

		Status = status;
		return this;
	}

	public AssignmentType UpdateInfo(
		string name,
		string? description,
		SubmissionType? defaultSubmissionType,
		decimal? defaultMarks,
		int? defaultDueDays,
		bool evaluationRequired,
		EvaluationMethod? defaultEvaluationMethod,
		TaskTypeStatus status
	)
	{
		SetName(name);
		SetDescription(description);
		SetDefaultSubmissionType(defaultSubmissionType);
		SetDefaultDueDays(defaultDueDays);
		SetEvaluationSettings(evaluationRequired, defaultEvaluationMethod, defaultMarks);
		SetStatus(status);
		return this;
	}
}

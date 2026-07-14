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
	public virtual EducationMedium AcademicMedium { get; }

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
		SetDefaultMarks(defaultMarks);
		SetDefaultDueDays(defaultDueDays);
		SetEvaluationRequired(evaluationRequired);
		SetDefaultEvaluationMethod(defaultEvaluationMethod);
		SetStatus(status);
	}

	/// <summary>
	/// Sets the name with validation
	/// </summary>
	internal AssignmentType SetName(string name)
	{
		Name = Check.NotNullOrWhiteSpace(
			name,
			nameof(name),
			maxLength: AssignmentTypeConsts.MaxNameLength
		);
		return this;
	}

	internal AssignmentType SetDescription(string? description)
	{
		if (description != null && description.Length > 250)
		{
			throw new ArgumentException("Description cannot exceed 250 characters.", nameof(description));
		}
		Description = description;
		return this;
	}

	internal AssignmentType SetDefaultSubmissionType(SubmissionType? defaultSubmissionType)
	{
		DefaultSubmissionType = defaultSubmissionType;
		return this;
	}

	internal AssignmentType SetDefaultMarks(decimal? defaultMarks)
	{
		if (defaultMarks.HasValue && defaultMarks.Value < 0)
		{
			throw new ArgumentException("Default marks cannot be negative.", nameof(defaultMarks));
		}
		DefaultMarks = defaultMarks;
		return this;
	}

	internal AssignmentType SetDefaultDueDays(int? defaultDueDays)
	{
		if (defaultDueDays.HasValue && defaultDueDays.Value < 0)
		{
			throw new ArgumentException("Default due days cannot be negative.", nameof(defaultDueDays));
		}
		DefaultDueDays = defaultDueDays;
		return this;
	}

	internal AssignmentType SetEvaluationRequired(bool evaluationRequired)
	{
		EvaluationRequired = evaluationRequired;
		return this;
	}

	internal AssignmentType SetDefaultEvaluationMethod(EvaluationMethod? defaultEvaluationMethod)
	{
		DefaultEvaluationMethod = defaultEvaluationMethod;
		return this;
	}

	internal AssignmentType SetStatus(TaskTypeStatus status)
	{
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
		SetDefaultMarks(defaultMarks);
		SetDefaultDueDays(defaultDueDays);
		SetEvaluationRequired(evaluationRequired);
		SetDefaultEvaluationMethod(defaultEvaluationMethod);
		SetStatus(status);
		return this;
	}
}

using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// Represents a homework schedule for a specific division
/// Supports scheduled publishing per division
/// </summary>
public class HomeworkDivisionSchedule : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }

	/// <summary>
	/// Parent homework ID
	/// </summary>
	public Guid HomeworkId { get; set; }

	/// <summary>
	/// Division ID
	/// </summary>
	public Guid DivisionId { get; set; }

	/// <summary>
	/// Scheduled auto-publish date (nullable for manual publish)
	/// </summary>
	public DateTime? PublishDate { get; set; }

	/// <summary>
	/// Due date for submission
	/// </summary>
	public DateTime DueDate { get; set; }

	/// <summary>
	/// Actual published timestamp (null = not yet published)
	/// </summary>
	public DateTime? PublishedAt { get; set; }

	/// <summary>
	/// Is this division's homework published?
	/// </summary>
	public bool IsPublished { get; set; }

	// Navigation property
	public virtual HomeworkAssignmentEntity Homework { get; set; } = null!;

	/// <summary>
	/// Private constructor for EF Core
	/// </summary>
	private HomeworkDivisionSchedule() { }

	/// <summary>
	/// Public constructor for creating homework division schedule
	/// </summary>
	public HomeworkDivisionSchedule(
		Guid id,
		Guid homeworkId,
		Guid divisionId,
		DateTime? publishDate,
		DateTime dueDate,
		Guid? tenantId = null
	)
		: base(id)
	{
		HomeworkId = homeworkId;
		DivisionId = Check.NotNull(divisionId, nameof(divisionId));
		PublishDate = publishDate;
		DueDate = dueDate;
		TenantId = tenantId;
		IsPublished = false;
		PublishedAt = null;
	}

	/// <summary>
	/// Publish this division's homework
	/// </summary>
	public void Publish()
	{
		IsPublished = true;
		PublishedAt = DateTime.UtcNow;
	}

	/// <summary>
	/// Update scheduling dates
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when trying to update published homework</exception>
	public void UpdateSchedule(DateTime? publishDate, DateTime dueDate)
	{
		if (IsPublished)
			throw new InvalidOperationException(
				"Cannot update schedule for published homework"
			);

		PublishDate = publishDate;
		DueDate = dueDate;
	}
}

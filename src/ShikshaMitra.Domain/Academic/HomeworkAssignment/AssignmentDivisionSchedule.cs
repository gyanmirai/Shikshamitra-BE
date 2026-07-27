using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// Represents an assignment schedule for a specific division
/// Supports scheduled publishing per division
/// </summary>
public class AssignmentDivisionSchedule : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }

	/// <summary>
	/// Parent assignment ID
	/// </summary>
	public Guid AssignmentId { get; set; }

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
	/// Is this division's assignment published?
	/// </summary>
	public bool IsPublished { get; set; }

	// Navigation property
	public virtual AssignmentEntity Assignment { get; set; } = null!;

	/// <summary>
	/// Private constructor for EF Core
	/// </summary>
	private AssignmentDivisionSchedule() { }

	/// <summary>
	/// Public constructor for creating assignment division schedule
	/// </summary>
	public AssignmentDivisionSchedule(
		Guid id,
		Guid assignmentId,
		Guid divisionId,
		DateTime? publishDate,
		DateTime dueDate,
		Guid? tenantId = null
	)
		: base(id)
	{
		AssignmentId = assignmentId;
		DivisionId = Check.NotNull(divisionId, nameof(divisionId));
		PublishDate = publishDate;
		DueDate = dueDate;
		TenantId = tenantId;
		IsPublished = false;
		PublishedAt = null;
	}

	/// <summary>
	/// Publish this division's assignment
	/// </summary>
	public void Publish(DateTime? publishedAt = null)
	{
		if (IsPublished)
		{
			return;
		}

		IsPublished = true;
		PublishedAt = publishedAt ?? DateTime.UtcNow;
	}

	/// <summary>
	/// Update scheduling dates
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when trying to update published assignment</exception>
	public void UpdateSchedule(DateTime? publishDate, DateTime dueDate)
	{
		if (IsPublished)
			throw new InvalidOperationException(
				"Cannot update schedule for published assignment"
			);

		PublishDate = publishDate;
		DueDate = dueDate;
	}
}

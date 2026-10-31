using System.Diagnostics.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Curriculum.Settings;

public class ElectiveGroup : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	#region Properties
	public Guid? TenantId { get; private set; }
	public string Name { get; private set; }
	public string Color { get; private set; }
	public string? GroupType { get; private set; }
	public string? Description { get; private set; }
	public SubjectMasterStatus Status { get; private set; }
	public int? SortOrder { get; private set; }
	public string? ArchiveReason { get; private set; }
	public virtual ICollection<ElectiveGroupComponent> Subjects { get; set; }
	public Guid AcademicMediumId { get; set; }
	public EducationMedium AcademicMedium { get; set; } = null!; // Populated by EF relationship materialization.
	#endregion

	#region Constructors
	private ElectiveGroup()
	{
		// EF materializes these required columns after this ORM constructor.
		Color = null!;
		Name = null!;

		/* This constructor is for deserialization / ORM purpose */
		Subjects = new HashSet<ElectiveGroupComponent>();
	}

	public ElectiveGroup(
		Guid id,
		Guid? tenantId,
		string name,
		string color,
		Guid educationMediumId,
		string? groupType = null,
		string? description = null,
		SubjectMasterStatus status = SubjectMasterStatus.Active,
		int? sortOrder = null
	)
		: base(id)
	{
		TenantId = tenantId;
		SetName(name);
		SetColor(color);
		SetDetails(groupType, description, status, sortOrder);
		AcademicMediumId = educationMediumId;
		Subjects = new HashSet<ElectiveGroupComponent>();
	}
	#endregion

	#region Methods
	[MemberNotNull(nameof(Name))]
	public void SetName(string name)
	{
		Check.NotNullOrWhiteSpace(name, nameof(name));
		Name = name;
	}

	[MemberNotNull(nameof(Color))]
	public void SetColor(string color)
	{
		Check.NotNullOrWhiteSpace(color, nameof(color));
		Color = color;
	}

	public void SetDetails(
		string? groupType,
		string? description,
		SubjectMasterStatus status,
		int? sortOrder
	)
	{
		GroupType = string.IsNullOrWhiteSpace(groupType) ? null : groupType.Trim();
		Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
		Status = status;
		SortOrder = sortOrder;
		if (status != SubjectMasterStatus.Archived)
		{
			ArchiveReason = null;
		}
	}

	public void Archive(string? reason)
	{
		Status = SubjectMasterStatus.Archived;
		ArchiveReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
	}

	public void Restore()
	{
		Status = SubjectMasterStatus.Active;
		ArchiveReason = null;
	}

	public void AddSubject(Guid subjectId)
	{
		if (IsInSubjects(subjectId))
		{
			return;
		}
		Subjects.Add(new ElectiveGroupComponent(Id, subjectId));
	}

	public void RemoveSubject(Guid subjectId)
	{
		var subjectToRemove = Subjects.FirstOrDefault(s => s.SubjectId == subjectId);
		if (subjectToRemove != null)
		{
			Subjects.Remove(subjectToRemove);
		}
	}

	public void ReplaceSubjects(IEnumerable<Guid> subjectIds)
	{
		Subjects.Clear();
		if (subjectIds == null)
		{
			return;
		}

		foreach (var subjectId in subjectIds.Distinct())
		{
			AddSubject(subjectId);
		}
	}

	private bool IsInSubjects(Guid subjectId) => Subjects.Any(s => s.SubjectId == subjectId);
	#endregion
}

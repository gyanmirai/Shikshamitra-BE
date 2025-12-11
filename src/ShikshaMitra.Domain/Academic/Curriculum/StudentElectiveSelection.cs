using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Represents a student's elective subject selection
/// </summary>
public class StudentElectiveSelection : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	
	/// <summary>
	/// Student ID
	/// </summary>
	public Guid StudentId { get; private set; }
	
	/// <summary>
	/// Elective Group ID
	/// </summary>
	public Guid ElectiveGroupId { get; private set; }
	
	/// <summary>
	/// Selected Subject ID from the elective group
	/// </summary>
	public Guid SelectedSubjectId { get; private set; }
	
	/// <summary>
	/// Academic Medium ID
	/// </summary>
	public Guid AcademicMediumId { get; private set; }
	
	/// <summary>
	/// Student's original preference (optional)
	/// </summary>
	public string? StudentPreference { get; private set; }

	private StudentElectiveSelection()
	{
		// Required for EF Core
	}

	public StudentElectiveSelection(
		Guid id,
		Guid? tenantId,
		Guid studentId,
		Guid electiveGroupId,
		Guid selectedSubjectId,
		Guid academicMediumId,
		string? studentPreference = null
	) : base(id)
	{
		TenantId = tenantId;
		StudentId = Check.NotDefaultOrNull<Guid>(studentId, nameof(studentId));
		ElectiveGroupId = Check.NotDefaultOrNull<Guid>(electiveGroupId, nameof(electiveGroupId));
		SelectedSubjectId = Check.NotDefaultOrNull<Guid>(selectedSubjectId, nameof(selectedSubjectId));
		AcademicMediumId = Check.NotDefaultOrNull<Guid>(academicMediumId, nameof(academicMediumId));
		StudentPreference = studentPreference;
	}

	public void UpdateSelection(Guid selectedSubjectId)
	{
		SelectedSubjectId = Check.NotDefaultOrNull<Guid>(selectedSubjectId, nameof(selectedSubjectId));
	}

	public void UpdatePreference(string? preference)
	{
		StudentPreference = preference;
	}
}

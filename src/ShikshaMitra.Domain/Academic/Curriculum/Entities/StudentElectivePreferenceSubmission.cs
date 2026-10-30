using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>Permanent receipt; school assignment changes must never modify this record.</summary>
public class StudentElectivePreferenceSubmission : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid ElectiveGroupId { get; private set; }
	public Guid PreferredSubjectId { get; private set; }
	public string PreferredSubjectName { get; private set; } = "";
	public Guid SubmittingUserId { get; private set; }
	public DateTime SubmittedAt { get; private set; }
	public Guid ClientRequestId { get; private set; }
	private StudentElectivePreferenceSubmission() { }
	public StudentElectivePreferenceSubmission(Guid id, Guid tenantId, Guid studentId, Guid academicYearId,
		Guid electiveGroupId, Guid preferredSubjectId, string preferredSubjectName, Guid submittingUserId,
		DateTime submittedAt, Guid clientRequestId) : base(id)
	{
		TenantId = Check.NotDefaultOrNull<Guid>(tenantId, nameof(tenantId));
		StudentId = Check.NotDefaultOrNull<Guid>(studentId, nameof(studentId));
		AcademicYearId = Check.NotDefaultOrNull<Guid>(academicYearId, nameof(academicYearId));
		ElectiveGroupId = Check.NotDefaultOrNull<Guid>(electiveGroupId, nameof(electiveGroupId));
		PreferredSubjectId = Check.NotDefaultOrNull<Guid>(preferredSubjectId, nameof(preferredSubjectId));
		PreferredSubjectName = Check.NotNullOrWhiteSpace(preferredSubjectName, nameof(preferredSubjectName), 512);
		SubmittingUserId = Check.NotDefaultOrNull<Guid>(submittingUserId, nameof(submittingUserId));
		SubmittedAt = submittedAt;
		ClientRequestId = Check.NotDefaultOrNull<Guid>(clientRequestId, nameof(clientRequestId));
	}
}


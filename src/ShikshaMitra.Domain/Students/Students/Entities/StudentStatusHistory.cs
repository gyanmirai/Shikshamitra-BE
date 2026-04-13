using System;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students;

/// <summary>
/// Immutable audit log entry that records every operational status transition for a student.
/// <para>
/// ABP automatically populates <see cref="Volo.Abp.Auditing.ICreationAuditedObject.CreatorId"/>
/// and <see cref="Volo.Abp.Auditing.IHasCreationTime.CreationTime"/> when the unit of work commits,
/// providing a full "who / when" trail without any extra plumbing.
/// </para>
/// </summary>
public class StudentStatusHistory : CreationAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }

	/// <summary>The student whose status changed.</summary>
	public Guid StudentId { get; private set; }

	/// <summary>Lifecycle status before this transition.</summary>
	public StudentLifeCycleStatusEnum FromStatus { get; private set; }

	/// <summary>Lifecycle status after this transition.</summary>
	public StudentLifeCycleStatusEnum ToStatus { get; private set; }

	/// <summary>
	/// Optional free-text explanation captured at the time of the change.
	/// Persisted verbatim; see <see cref="StudentStatusHistoryConsts.MaxRemarksLength"/> for the column cap.
	/// </summary>
	public string? Remarks { get; private set; }

	private StudentStatusHistory() { /* required by ORM */ }

	public StudentStatusHistory(
		Guid id,
		Guid? tenantId,
		Guid studentId,
		StudentLifeCycleStatusEnum fromStatus,
		StudentLifeCycleStatusEnum toStatus,
		string? remarks)
		: base(id)
	{
		TenantId = tenantId;
		StudentId = studentId;
		FromStatus = fromStatus;
		ToStatus = toStatus;
		Remarks = remarks;
	}
}

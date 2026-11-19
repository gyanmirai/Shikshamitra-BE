using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Attendance;

public class StudentAttendanceCorrection : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AttendanceDayId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid RequestedBy { get; private set; }
	public Guid? ReviewedBy { get; private set; }
	public Guid OperationId { get; private set; }
	public string BeforeJson { get; private set; } = "";
	public string RequestJson { get; private set; } = "";
	public string PolicyJson { get; private set; } = "";
	public string Reason { get; private set; } = "";
	public string? ReviewReason { get; private set; }
	public string Status { get; private set; } = "Pending";
	protected StudentAttendanceCorrection() { }
	public StudentAttendanceCorrection(Guid id, Guid? tenantId, Guid dayId, Guid studentId, Guid requestedBy,
		Guid operationId, string beforeJson, string requestJson, string policyJson, string reason) : base(id)
	{
		TenantId = tenantId; AttendanceDayId = dayId; StudentId = studentId; RequestedBy = requestedBy;
		OperationId = operationId == Guid.Empty ? id : operationId;
		BeforeJson = beforeJson; RequestJson = requestJson; PolicyJson = policyJson;
		Reason = Check.NotNullOrWhiteSpace(reason, nameof(reason), 2000);
	}
	public void Apply(Guid actor, string reason, bool reviewed)
	{
		RequirePending();
		if (reviewed && actor == RequestedBy) throw new BusinessException(AttendanceOperationErrorCodes.SelfApproval);
		ReviewedBy = actor; ReviewReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), 2000); Status = "Applied";
	}
	public void Reject(Guid actor, string reason)
	{
		RequirePending();
		if (actor == RequestedBy) throw new BusinessException(AttendanceOperationErrorCodes.SelfApproval);
		ReviewedBy = actor; ReviewReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), 2000); Status = "Rejected";
	}
	private void RequirePending() { if (Status != "Pending") throw new BusinessException(AttendanceOperationErrorCodes.CorrectionAlreadyReviewed); }
}

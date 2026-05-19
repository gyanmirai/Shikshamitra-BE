using System;
using ShikshaMitra.HR.AttendanceAndLeaves;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class AttendancePolicyEvaluationResult : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AttendancePolicyEvaluationRunId { get; private set; }
	public Guid EmployeeId { get; private set; }
	public DateOnly AttendanceDate { get; private set; }
	public Guid? SourceAttendanceId { get; private set; }
	public EmployeeAttendanceStatusType BaselineStatus { get; private set; }
	public decimal BaselinePayableUnits { get; private set; }
	public EmployeeAttendanceStatusType EvaluatedStatus { get; private set; }
	public decimal EvaluatedPayableUnits { get; private set; }
	public bool IsLate { get; private set; }
	public bool IsEarlyExit { get; private set; }
	public int OvertimeMinutes { get; private set; }
	public string WarningsJson { get; private set; } = "[]";
	public bool IsManualOverride { get; private set; }
	public AttendancePolicyEvaluationRun Run { get; private set; } = null!;

	private AttendancePolicyEvaluationResult()
	{
	}

	public AttendancePolicyEvaluationResult(
		Guid id,
		Guid? tenantId,
		Guid attendancePolicyEvaluationRunId,
		Guid employeeId,
		DateOnly attendanceDate,
		Guid? sourceAttendanceId,
		EmployeeAttendanceStatusType baselineStatus,
		decimal baselinePayableUnits,
		EmployeeAttendanceStatusType evaluatedStatus,
		decimal evaluatedPayableUnits,
		bool isLate,
		bool isEarlyExit,
		int overtimeMinutes,
		string warningsJson,
		bool isManualOverride) : base(id)
	{
		TenantId = tenantId;
		AttendancePolicyEvaluationRunId = attendancePolicyEvaluationRunId;
		EmployeeId = employeeId;
		AttendanceDate = attendanceDate;
		SourceAttendanceId = sourceAttendanceId;
		BaselineStatus = baselineStatus;
		BaselinePayableUnits = baselinePayableUnits;
		EvaluatedStatus = evaluatedStatus;
		EvaluatedPayableUnits = evaluatedPayableUnits;
		IsLate = isLate;
		IsEarlyExit = isEarlyExit;
		OvertimeMinutes = overtimeMinutes;
		WarningsJson = warningsJson;
		IsManualOverride = isManualOverride;
	}
}

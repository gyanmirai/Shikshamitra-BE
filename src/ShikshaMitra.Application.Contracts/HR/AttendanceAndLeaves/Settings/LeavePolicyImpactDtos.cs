using System;
using System.Collections.Generic;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class LeavePolicyImpactRequest
{
	public Guid AcademicYearId { get; set; }
	public Guid? PolicyId { get; set; }
	public string Action { get; set; } = "update";
	public DateTime AsOfDate { get; set; }
	public DateTime? FromDate { get; set; }
	public DateTime? ToDate { get; set; }
	public int? ExpectedVersion { get; set; }
	public LeavePolicyCreateRequest? Proposal { get; set; }
}

public class LeaveImpactMeasure
{
	public string Status { get; set; } = "not_calculated";
	public int? Count { get; set; }
	public string Definition { get; set; } = string.Empty;
	public string Scope { get; set; } = "requested_window";
	public string? AvailabilityReason { get; set; }
	public string? DrilldownKey { get; set; }
}

public class LeavePayrollExposurePeriodDto
{
	public string Month { get; set; } = string.Empty;
	public int SnapshotEmployeeCount { get; set; }
	public int SettlementEmployeeCount { get; set; }
	public int LockedSnapshotEmployeeCount { get; set; }
	public string LockState { get; set; } = "no_snapshot";
}

public class LeavePolicyImpactDto
{
	public DateTime CalculatedAt { get; set; }
	public DateTime AsOfDate { get; set; }
	public DateTime FromDate { get; set; }
	public DateTime ToDate { get; set; }
	public string Completeness { get; set; } = "not_calculated";
	public string Fingerprint { get; set; } = string.Empty;
	public string? Limitation { get; set; }
	public List<LeavePayrollExposurePeriodDto> PayrollPeriods { get; set; } = new();
	public Dictionary<string, LeaveImpactMeasure> Measures { get; set; } = new();
}

using System.Collections.Generic;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public class RunPayrollReadinessSummary
{
	public double NetPayTotal { get; set; }
	public string Month { get; set; } = string.Empty;
	public bool CanPreview { get; set; }
	public bool CanLock { get; set; }
	public int EmployeeCount { get; set; }
	public int SnapshotCount { get; set; }
	public int ReviewedCount { get; set; }
	public int LockedCount { get; set; }
	public int AttendanceFoundCount { get; set; }
	public int MissingAttendanceCount { get; set; }
	public int MissingSalaryStructureCount { get; set; }
	public int PolicyConfigurationIssueCount { get; set; }
	public List<string> BlockingIssues { get; set; } = new();
	public List<string> RecommendedActions { get; set; } = new();
}

using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class AttendanceConfigurationRequest
{
	[Required]
	public AttendanceDailyRulesRequest DailyRules { get; set; } = new();

	[Required]
	public AttendanceLateEarlyRulesRequest LateEarlyRules { get; set; } = new();

	[Required]
	public AttendanceOvertimeRulesRequest Overtime { get; set; } = new();
}

public class AttendanceDailyRulesRequest
{
	[Range(0, 100)]
	public double HoursBasedPayPercentage { get; set; }
}

public class AttendanceLateEarlyRulesRequest
{
	[Range(0, int.MaxValue)]
	public int GraceTime { get; set; }

	[Range(0, int.MaxValue)]
	public int MaxLateAllowedPerMonth { get; set; }

	[Required]
	[RegularExpression("^(none|deduct_half_day|deduct_full_day)$")]
	public string PenaltyType { get; set; } = "none";
}

public class AttendanceOvertimeRulesRequest
{
	public bool Enabled { get; set; }

	[RegularExpression("^(1.5x|2.0x|flat)$")]
	public string? Rate { get; set; }

	public int? MinOvertimeMinutes { get; set; }
}

public class AttendanceConfigurationResponse
{
	public bool Success { get; set; }
	public string Message { get; set; } = string.Empty;
	public AttendanceDailyRulesRequest? DailyRules { get; set; }
	public AttendanceLateEarlyRulesRequest? LateEarlyRules { get; set; }
	public AttendanceOvertimeRulesRequest? Overtime { get; set; }
}

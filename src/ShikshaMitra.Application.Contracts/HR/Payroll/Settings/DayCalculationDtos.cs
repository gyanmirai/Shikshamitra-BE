namespace ShikshaMitra.HR.Payroll.Settings;

public class DayCalculationRequestDto
{
	public string DayCalculationType { get; set; } = string.Empty;
	public int? ManualDays { get; set; }
}

public class DayCalculationResponseDto
{
	public bool Success { get; set; }
	public string Message { get; set; } = string.Empty;
}

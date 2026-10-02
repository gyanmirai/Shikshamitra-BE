using System;
using System.Linq;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Attendance;

public sealed class StudentAttendanceSchedule : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid EnrollmentId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public string Name { get; private set; } = "";
	public string Kind { get; private set; } = "Fixed";
	public DateTime StartDate { get; private set; }
	public DateTime? EndDate { get; private set; }
	public string WeekDaysJson { get; private set; } = "[]";
	public string? StartTime { get; private set; }
	public string? EndTime { get; private set; }
	public DateTime? RetiredFrom { get; private set; }
	private StudentAttendanceSchedule() { }
	public StudentAttendanceSchedule(Guid id, Guid tenantId, Guid enrollmentId, Guid academicYearId, string name,
		string kind, DateTime startDate, DateTime? endDate, int[] weekDays, string? startTime, string? endTime) : base(id)
	{
		TenantId = tenantId; EnrollmentId = enrollmentId; AcademicYearId = academicYearId;
		Update(name, kind, startDate, endDate, weekDays, startTime, endTime);
	}
	public void Update(string name, string kind, DateTime startDate, DateTime? endDate, int[] weekDays, string? startTime, string? endTime)
	{
		if (string.IsNullOrWhiteSpace(name) || name.Length > 150 || kind is not ("Fixed" or "Temporary") ||
			endDate?.Date < startDate.Date || (kind == "Temporary" && !endDate.HasValue) || weekDays == null || weekDays.Length == 0 ||
			weekDays.Any(x => x < 0 || x > 6) || weekDays.Distinct().Count() != weekDays.Length ||
			(startTime == null) != (endTime == null) || (startTime != null &&
			(!TimeOnly.TryParseExact(startTime, "HH:mm", out var start) || !TimeOnly.TryParseExact(endTime, "HH:mm", out var end) || end <= start)))
			throw new BusinessException(AttendancePolicyErrorCodes.InvalidSchedule);
		Name = name.Trim(); Kind = kind; StartDate = startDate.Date; EndDate = endDate?.Date;
		WeekDaysJson = JsonSerializer.Serialize(weekDays.Order().ToArray()); StartTime = startTime; EndTime = endTime;
	}
	public int[] WeekDays() => JsonSerializer.Deserialize<int[]>(WeekDaysJson)!;
	public bool InRange(DateTime date) => date.Date >= StartDate && (!EndDate.HasValue || date.Date <= EndDate.Value) && (!RetiredFrom.HasValue || date.Date < RetiredFrom.Value);
	public void Retire(DateTime from) => RetiredFrom = from.Date;
	public bool Expected(DateTime date) => InRange(date) && WeekDays().Contains((int)date.DayOfWeek);
	public bool Overlaps(StudentAttendanceSchedule other) => EnrollmentId == other.EnrollmentId && Kind == other.Kind &&
		StartDate <= other.EffectiveEnd() && other.StartDate <= EffectiveEnd();
	private DateTime EffectiveEnd() => RetiredFrom.HasValue && RetiredFrom.Value <= (EndDate ?? DateTime.MaxValue)
		? RetiredFrom.Value.AddDays(-1) : EndDate ?? DateTime.MaxValue;
}

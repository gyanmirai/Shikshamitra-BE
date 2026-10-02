using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Attendance;

public sealed class AttendanceScheduleDto
{
	public Guid Id { get; set; }
	public Guid EnrollmentId { get; set; }
	public Guid AcademicYearId { get; set; }
	public string Name { get; set; } = "";
	public string Kind { get; set; } = "Fixed";
	public DateTime StartDate { get; set; }
	public DateTime? EndDate { get; set; }
	public int[] WeekDays { get; set; } = [];
	public string? StartTime { get; set; }
	public string? EndTime { get; set; }
	public string ConcurrencyStamp { get; set; } = "";
	public DateTime? RetiredFrom { get; set; }
}
public sealed class SaveAttendanceScheduleDto
{
	public Guid? Id { get; set; }
	public Guid EnrollmentId { get; set; }
	[Required, MaxLength(150)] public string Name { get; set; } = "";
	[Required] public string Kind { get; set; } = "Fixed";
	public DateTime StartDate { get; set; }
	public DateTime? EndDate { get; set; }
	[Required] public int[] WeekDays { get; set; } = [];
	public string? StartTime { get; set; }
	public string? EndTime { get; set; }
	public string? ConcurrencyStamp { get; set; }
}
public interface IAttendanceScheduleAppService : IApplicationService
{
	Task<ListResultDto<AttendanceScheduleDto>> GetListAsync(Guid enrollmentId);
	Task<AttendanceScheduleDto> SaveAsync(SaveAttendanceScheduleDto input);
	Task DeleteAsync(Guid id, string concurrencyStamp);
}

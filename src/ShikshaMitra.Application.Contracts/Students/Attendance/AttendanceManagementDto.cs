using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Attendance;

public class AttendanceRegisterInput : PagedResultRequestDto
{
	public Guid AcademicYearId { get; set; }
	public Guid? StandardId { get; set; }
	public Guid? DivisionId { get; set; }
	public DateTime StartDate { get; set; }
	public DateTime EndDate { get; set; }
}
public class AttendanceSummaryInput
{
	public Guid StudentId { get; set; }
	public Guid AcademicYearId { get; set; }
	public DateTime StartDate { get; set; }
	public DateTime EndDate { get; set; }
}
public class AttendanceSummaryDto
{
	public Guid StudentId { get; set; }
	public Guid EnrollmentId { get; set; }
	public Guid AcademicYearId { get; set; }
	public string FullName { get; set; } = "";
	public int? RollNo { get; set; }
	public int ExpectedDays { get; set; }
	public int RecordedDays { get; set; }
	public int MissingDays { get; set; }
	public double PresentCredit { get; set; }
	public int PresentDays { get; set; }
	public double DenominatorDays { get; set; }
	public double? Percentage { get; set; }
	public bool IsProvisional { get; set; }
	public decimal LowAttendanceThreshold { get; set; } = 75m;
	public bool IsBelowThreshold { get; set; }
	public List<AttendanceSummaryDayDto> Daily { get; set; } = [];
}
public class AttendanceSummaryDayDto
{
	public DateTime Date { get; set; }
	public AttendanceStatus Status { get; set; }
	public bool Late { get; set; }
	public bool HalfDay { get; set; }
	public string? Remarks { get; set; }
	public bool Expected { get; set; }
	public string? ExclusionReason { get; set; }
	public double PresentCredit { get; set; }
	public double DenominatorCredit { get; set; } = 1;
	public bool Recorded { get; set; }
	public bool IsProvisional { get; set; }
}
public class AttendanceRegisterDto
{
	public long TotalCount { get; set; }
	public List<AttendanceSummaryDto> Rows { get; set; } = [];
}
public class AttendanceCorrectionInput : GetStudentsForAttendanceInput
{
	public Guid StudentId { get; set; }
	public AttendanceStatus Status { get; set; }
	public bool Late { get; set; }
	public bool HalfDay { get; set; }
	[MaxLength(500)] public string? Remarks { get; set; }
	[Required, MaxLength(2000)] public string Reason { get; set; } = "";
	public string Revision { get; set; } = "";
	public Guid OperationId { get; set; }
}
public class AttendanceCorrectionListInput : PagedAndSortedResultRequestDto
{
	public Guid? AcademicYearId { get; set; }
	public Guid? StandardId { get; set; }
	public Guid? DivisionId { get; set; }
	public string? Status { get; set; }
}
public class AttendanceCorrectionDecisionInput
{
	[Required] public string Revision { get; set; } = "";
	[Required, MaxLength(2000)] public string Reason { get; set; } = "";
}
public class AttendanceCorrectionDto
{
	public Guid Id { get; set; }
	public Guid StudentId { get; set; }
	public string StudentName { get; set; } = "";
	public Guid AcademicYearId { get; set; }
	public Guid StandardId { get; set; }
	public Guid DivisionId { get; set; }
	public DateTime Date { get; set; }
	public AttendanceStatus BeforeStatus { get; set; }
	public AttendanceStatus RequestedStatus { get; set; }
	public bool IsCreditCorrection { get; set; }
	public double? BeforePresentCredit { get; set; }
	public double? BeforeDenominatorCredit { get; set; }
	public double? RequestedPresentCredit { get; set; }
	public double? RequestedDenominatorCredit { get; set; }
	public bool Late { get; set; }
	public bool HalfDay { get; set; }
	public string? Remarks { get; set; }
	public string Reason { get; set; } = "";
	public string Status { get; set; } = "Pending";
	public string Revision { get; set; } = "";
}
public interface IAttendanceManagementAppService : IApplicationService
{
	Task<AttendanceRegisterDto> GetRegisterAsync(AttendanceRegisterInput input);
	Task<AttendanceSummaryDto> GetSummaryAsync(AttendanceSummaryInput input);
	Task<PagedResultDto<AttendanceCorrectionDto>> GetCorrectionsAsync(AttendanceCorrectionListInput input);
	Task<AttendanceCorrectionDto> RequestCorrectionAsync(AttendanceCorrectionInput input);
	Task<AttendanceCorrectionDto> ApproveCorrectionAsync(Guid id, AttendanceCorrectionDecisionInput input);
	Task<AttendanceCorrectionDto> RejectCorrectionAsync(Guid id, AttendanceCorrectionDecisionInput input);
}

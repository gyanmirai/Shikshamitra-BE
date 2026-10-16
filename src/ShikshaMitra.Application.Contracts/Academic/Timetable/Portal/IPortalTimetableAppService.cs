using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using ShikshaMitra.Students;

namespace ShikshaMitra.Academic.Timetable.Portal;

public interface IPortalTimetableAppService
{
    Task<PortalTimetableContextDto> GetContextAsync(Guid? studentId = null, Guid? academicYearId = null);
    Task<PortalTimetableScheduleDto> GetScheduleAsync(PortalTimetableScheduleInput input);
}

public class PortalTimetableScheduleInput
{
    public Guid? StudentId { get; set; }
    [Required] public Guid AcademicYearId { get; set; }
    [Required] public DateOnly FromDate { get; set; }
    [Required] public DateOnly ToDate { get; set; }
}

public class PortalTimetableContextDto
{
    public PortalTimetableStudentDto Student { get; set; } = new();
    public PortalTimetableYearDto? CurrentAcademicYear { get; set; }
    public List<PortalTimetableYearDto> AvailableAcademicYears { get; set; } = [];
    public List<string> WorkingDays { get; set; } = [];
    public string Timezone { get; set; } = "Asia/Kolkata";
    public string Availability { get; set; } = "noEnrollment";
}

public class PortalTimetableStudentDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public GenderEnum? Gender { get; set; }
    public Guid? MediumId { get; set; }
    public string? MediumName { get; set; }
    public Guid? StageId { get; set; }
    public string? StageName { get; set; }
    public Guid? StandardId { get; set; }
    public string? StandardName { get; set; }
    public Guid? DivisionId { get; set; }
    public string? DivisionName { get; set; }
    public int? RollNumber { get; set; }
}

public class PortalTimetableYearDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}

public class PortalTimetableScheduleDto
{
    public Guid StudentId { get; set; }
    public Guid AcademicYearId { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public string Timezone { get; set; } = "Asia/Kolkata";
    public string Availability { get; set; } = "available";
    public List<PortalTimetableDayDto> Days { get; set; } = [];
}

public class PortalTimetableDayDto
{
    public DateOnly Date { get; set; }
    public bool IsWorkingDay { get; set; }
    public string DayType { get; set; } = "regular";
    public string? DayTitle { get; set; }
    public List<PortalTimetablePeriodDto> Periods { get; set; } = [];
}

public class PortalTimetablePeriodDto
{
    public Guid PeriodId { get; set; }
    public int? PeriodNumber { get; set; }
    public string PeriodType { get; set; } = "class";
    public string? Title { get; set; }
    public string? BreakType { get; set; }
    public PortalTimetableSubjectDto? Subject { get; set; }
    public PortalTimetableReferenceDto? Teacher { get; set; }
    public PortalTimetableReferenceDto? ReplacementTeacher { get; set; }
    public PortalTimetableReferenceDto? Room { get; set; }
    public DateTimeOffset StartDateTime { get; set; }
    public DateTimeOffset EndDateTime { get; set; }
    public string Status { get; set; } = "scheduled";
    public bool CanMessageTeacher { get; set; }
}

public class PortalTimetableReferenceDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class PortalTimetableSubjectDto : PortalTimetableReferenceDto
{
    public string? Code { get; set; }
    public string? Color { get; set; }
}

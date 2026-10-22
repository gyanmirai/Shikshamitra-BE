using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace ShikshaMitra.Academic.Mobile;

public class MobileAcademicInput
{
	public Guid? StudentId { get; set; }
	public Guid? AcademicYearId { get; set; }
}

public class MobileListInput : MobileAcademicInput
{
	[Range(1, int.MaxValue)] public int PageNumber { get; set; } = 1;
	[Range(1, 100)] public int PageSize { get; set; } = 20;
	public string? Search { get; set; }
	public string? Status { get; set; }
	public string? Tab { get; set; }
	public Guid? SubjectId { get; set; }
	public DateOnly? Date { get; set; }
}

public class MobilePage<T>
{
	public Guid StudentId { get; set; }
	public Guid AcademicYearId { get; set; }
	public List<T> Items { get; set; } = [];
	public int TotalCount { get; set; }
	public int PageNumber { get; set; }
	public int PageSize { get; set; }
}

public record MobileYearDto(Guid Id, string Name, DateOnly StartDate, DateOnly EndDate);
public record MobileStudentDto(Guid Id, string Name, Guid StandardId, string StandardName, Guid DivisionId, string DivisionName, int? RollNumber);
public record MobileContextDto(MobileStudentDto Student, MobileYearDto CurrentAcademicYear, List<MobileYearDto> AvailableAcademicYears);
public record MobileAttachmentDto(Guid Id, string Name, string Url, string ContentType, long Size, string? FileName = null);
public record MobileFileDto(byte[] Content, string Name, string ContentType);
public record MobileRubricScoreDto(string Title, decimal MaxScore, decimal? Score);

public record MobileHomeworkSubjectDto(Guid Id, string Name, string Code);

public class MobileHomeworkDto
{
	public Guid Id { get; set; }
	public string Type { get; set; } = "homework";
	public Guid SubjectId { get; set; }
	public string SubjectName { get; set; } = "";
	public string SubjectCode { get; set; } = "";
	public string Title { get; set; } = "";
	public string Description { get; set; } = "";
	public string TeacherName { get; set; } = "";
	public string SubmissionType { get; set; } = "online";
	public string Status { get; set; } = "pending";
	public DateTime AssignedDate { get; set; }
	public DateTime DueDate { get; set; }
	public DateTimeOffset? SubmittedDate { get; set; }
	public decimal? Marks { get; set; }
	public decimal? MaxMarks { get; set; }
	public bool CanSubmit { get; set; }
	public bool CanEdit { get; set; }
	public bool Acknowledged { get; set; }
	public string? TextAnswer { get; set; }
	public string? TextAnswerHtml { get; set; }
	public string Revision { get; set; } = "0";
	public DateTimeOffset? SavedAt { get; set; }
	public DateTimeOffset? UpdatedAt { get; set; }
	public bool CanSaveDraft { get; set; }
	public string? SubmissionReasonCode { get; set; }
	public string? Feedback { get; set; }
	public DateTimeOffset? ReviewedOn { get; set; }
	public List<MobileAttachmentDto> Attachments { get; set; } = [];
	public List<MobileAttachmentDto> SubmissionAttachments { get; set; } = [];
	public List<MobileRubricScoreDto> Rubric { get; set; } = [];
}

public class MobileSubmissionInput : MobileAcademicInput
{
	public bool Acknowledged { get; set; }
	[StringLength(100000)] public string? TextAnswer { get; set; }
	[StringLength(1048576)] public string? TextAnswerHtml { get; set; }
	[StringLength(40)] public string? ExpectedRevision { get; set; }
	public Guid? ClientRequestId { get; set; }
	public List<Guid> AttachmentIds { get; set; } = [];
}

public record MobileAttendanceDayDto(DateOnly Date, string Status, Guid? LeaveRequestId, string? Note, string? Reason);
public record MobileAttendanceOverviewDto(Guid StudentId, Guid AcademicYearId, int TotalPresent, int TotalAbsent, int TotalHalfDay, int TotalLate, double? CurrentPercentage, double? GoalPercentage);
public class MobileCalendarInput : MobileAcademicInput
{
	[Range(1900, 9998)] public int Year { get; set; }
	[Range(1, 12)] public int Month { get; set; }
}
public class MobileLeaveInput : MobileAcademicInput
{
	public Guid LeaveTypeId { get; set; }
	public DateOnly StartDate { get; set; }
	public DateOnly EndDate { get; set; }
	public bool IsHalfDay { get; set; }
	public TimeOnly? HalfDayStartTime { get; set; }
	public TimeOnly? HalfDayEndTime { get; set; }
	[Required, StringLength(2000)] public string Description { get; set; } = "";
	public List<Guid> AttachmentIds { get; set; } = [];
}
public record MobileLeaveTypeDto(Guid Id, string Name);
public record MobileLeaveDto(Guid Id, Guid LeaveTypeId, string Reason, DateOnly StartDate, DateOnly EndDate, decimal DurationDays, bool IsHalfDay, string Description, string Status, DateTime SubmittedAt, DateTime? DecisionAt, string? Remarks, bool CanEdit, bool CanCancel, List<MobileAttachmentDto> Attachments, string StudentName, string GradeSection, TimeOnly? HalfDayStartTime, TimeOnly? HalfDayEndTime);
public class MobileAbsenceInput : MobileAcademicInput
{
	[Required, StringLength(500)] public string Reason { get; set; } = "";
}

public class MobileChapterDto
{
	public Guid Id { get; set; }
	public Guid StandardId { get; set; }
	public Guid SubjectId { get; set; }
	public int ChapterNumber { get; set; }
	public string ChapterName { get; set; } = "";
	public string? Description { get; set; }
	public string? Topic { get; set; }
	public int? EstimatedTeachingDays { get; set; }
	public int? EstimatedPeriods { get; set; }
	public string? DifficultyLevel { get; set; }
	public string? BloomLevel { get; set; }
	public string? Tags { get; set; }
	public string? ClassActivities { get; set; }
	public string? CompletionCriteria { get; set; }
	public string? AssessmentMethod { get; set; }
	public string? QuestionTypes { get; set; }
	public int? MarksWeightage { get; set; }
	public string? UnitName { get; set; }
	public string? TermName { get; set; }
	public DateTimeOffset ChapterStartTime { get; set; }
	public DateTimeOffset ChapterEndTime { get; set; }
	public string? LearningOutcomes { get; set; }
	public string? SkillsCovered { get; set; }
	public string? Prerequisites { get; set; }
	public string? StudentSummary { get; set; }
	public string? ParentSummary { get; set; }
	public int ProgressPercentage { get; set; }
	public string CoverageStatus { get; set; } = "notStarted";
	public List<MobileAttachmentDto> Resources { get; set; } = [];
}
public record MobileChapterSummaryDto(int TotalChapters, int CompletedChapters, int InProgressChapters, int UpcomingChapters, int CoveragePercentage);
public class MobileChapterPageDto : MobilePage<MobileChapterDto>
{
	public MobileChapterSummaryDto Summary { get; set; } = new(0, 0, 0, 0, 0);
}
public record MobileSubjectProgressDto(Guid Id, string Name, string Code, int CoveragePercentage, int TotalChapters, int CompletedChapters, int InProgressChapters, int UpcomingChapters, string? Color = null);
public record MobileSyllabusDto(Guid StudentId, Guid AcademicYearId, string StudentName, string Grade, int OverallCoveragePercentage, int ChaptersCovered, int TotalChapters, List<MobileSubjectProgressDto> Subjects);

public class MobileExamSubjectDto
{
	public Guid Id { get; set; }
	public Guid SubjectId { get; set; }
	public string SubjectName { get; set; } = "";
	public DateTime? ExamDate { get; set; }
	public DateTime? StartTime { get; set; }
	public DateTime? EndTime { get; set; }
	public double? TotalMarks { get; set; }
	public double? PassingMarks { get; set; }
	public string? Notes { get; set; }
}
public record MobileExamDto(Guid Id, string Title, string AcademicYear, string ClassName, string Status, string ResultStatus, int DaysUntilStart, List<MobileExamSubjectDto> Schedule);
public record MobileSeatingDto(string Availability, string? RoomName, int? SeatNumber, string? Building, int? RollNumber);
public class MobileResultDto
{
  public string StudentName { get; set; } = "";
  public string GradeSection { get; set; } = "";
	public Guid Id { get; set; }
	public Guid ExamId { get; set; }
	public string ExamTitle { get; set; } = "";
	public string AcademicYear { get; set; } = "";
	public DateTime? ExamDate { get; set; }
	public DateTime? PublishedDate { get; set; }
	public string Status { get; set; } = "pending";
	public double? OverallObtained { get; set; }
	public double? OverallTotal { get; set; }
	public double? Percentage { get; set; }
	public string? Grade { get; set; }
	public string? PassStatus { get; set; }
	public int? Rank { get; set; }
	public List<MobileSubjectResultDto> Subjects { get; set; } = [];
}
public record MobileSubjectResultDto(Guid SubjectId, string SubjectName, double? ObtainedMarks, double? TotalMarks, double? Percentage, string? Status, string? TeacherRemark, double? ClassAverage = null, double? HighestPercentage = null);
public record MobileSchoolMarkerDto(Guid Id, string Type, DateOnly Date, string Title, string? Description, string Audience, string? Category, DateTimeOffset? StartAt, DateTimeOffset? EndAt, string? Venue, bool SchoolClosed);
public record MobileDashboardDto(Guid StudentId, Guid AcademicYearId, int PendingHomework, MobileAttendanceOverviewDto Attendance, List<MobileExamDto> UpcomingExams, List<MobileSchoolMarkerDto> UpcomingEvents);

public interface IMobileAcademicContextAppService : IApplicationService
{
	Task<MobileContextDto> GetAsync(MobileAcademicInput input);
}
public interface IMobileHomeworkAppService : IApplicationService
{
	Task<List<MobileHomeworkSubjectDto>> GetSubjectsAsync(MobileAcademicInput input);
	Task<MobilePage<MobileHomeworkDto>> GetListAsync(MobileListInput input);
	Task<MobileHomeworkDto> GetAsync(Guid id, MobileAcademicInput input);
	Task<MobileHomeworkDto> SaveAsync(Guid id, MobileSubmissionInput input, bool draft, bool replace);
	Task<MobileAttachmentDto> UploadAsync(Guid id, MobileAcademicInput input, IRemoteStreamContent file, Guid? clientUploadId = null);
	Task DeleteAttachmentAsync(Guid id, Guid attachmentId, MobileAcademicInput input);
	Task<MobileFileDto> DownloadAsync(Guid id, Guid attachmentId, MobileAcademicInput input);
}
public interface IMobileAttendanceAppService : IApplicationService
{
	Task<MobileAttendanceOverviewDto> GetOverviewAsync(MobileAcademicInput input);
	Task<List<MobileAttendanceDayDto>> GetCalendarAsync(MobileCalendarInput input);
	Task<MobileAttendanceDayDto> SetAbsenceReasonAsync(DateOnly date, MobileAbsenceInput input);
	Task<List<MobileLeaveTypeDto>> GetLeaveTypesAsync(MobileAcademicInput input);
	Task<MobilePage<MobileLeaveDto>> GetLeavesAsync(MobileListInput input);
	Task<MobileLeaveDto> GetLeaveAsync(Guid id, MobileAcademicInput input);
	Task<MobileLeaveDto> SaveLeaveAsync(Guid? id, MobileLeaveInput input);
	Task<MobileLeaveDto> CancelLeaveAsync(Guid id, MobileAcademicInput input);
	Task<MobileAttachmentDto> UploadAsync(MobileAcademicInput input, IRemoteStreamContent file);
	Task DeleteAttachmentAsync(Guid attachmentId, MobileAcademicInput input);
	Task<MobileFileDto> DownloadAsync(Guid id, Guid attachmentId, MobileAcademicInput input);
}
public interface IMobileSyllabusAppService : IApplicationService
{
	Task<MobileSyllabusDto> GetDashboardAsync(MobileAcademicInput input);
	Task<MobileChapterPageDto> GetChaptersAsync(Guid subjectId, MobileListInput input);
	Task<MobileChapterDto> GetChapterAsync(Guid id, MobileAcademicInput input);
	Task<MobileFileDto> DownloadAsync(Guid chapterId, Guid resourceId, MobileAcademicInput input);
}
public interface IMobileExamAppService : IApplicationService
{
	Task<MobilePage<MobileExamDto>> GetListAsync(MobileListInput input);
	Task<MobileExamDto> GetAsync(Guid id, MobileAcademicInput input);
	Task<MobileExamSubjectDto> GetPreparationAsync(Guid id, Guid scheduleId, MobileAcademicInput input);
	Task<MobileSeatingDto> GetSeatingAsync(Guid id, Guid scheduleId, MobileAcademicInput input);
	Task<List<MobileAttachmentDto>> GetResourcesAsync(Guid id, Guid scheduleId, MobileAcademicInput input, string type);
	Task<MobileFileDto> DownloadAsync(Guid id, Guid scheduleId, Guid resourceId, MobileAcademicInput input);
}
public interface IMobileResultAppService : IApplicationService
{
	Task<List<MobileYearDto>> GetYearsAsync(MobileAcademicInput input);
	Task<MobilePage<MobileResultDto>> GetListAsync(MobileListInput input);
	Task<MobileResultDto> GetAsync(Guid id, MobileAcademicInput input);
	Task<MobileSubjectResultDto> GetSubjectAsync(Guid id, Guid subjectId, MobileAcademicInput input);
	Task<MobileFileDto> DownloadReportCardAsync(Guid id, MobileAcademicInput input);
}
public interface IMobileCalendarAppService : IApplicationService
{
	Task<List<MobileSchoolMarkerDto>> GetAsync(MobileCalendarInput input);
	Task<MobileSchoolMarkerDto> GetMarkerAsync(Guid id, MobileAcademicInput input);
}
public interface IMobileDashboardAppService : IApplicationService
{
	Task<MobileDashboardDto> GetAsync(MobileAcademicInput input);
}

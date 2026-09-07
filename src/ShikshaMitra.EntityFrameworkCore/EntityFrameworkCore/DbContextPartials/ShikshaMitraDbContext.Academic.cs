using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Results;
using ShikshaMitra.Academic.Examination.SeatAllocation;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.Academic.Homework;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Academic.QuestionBank;
using ShikshaMitra.Academic.QuestionPapers;
using ShikshaMitra.Academic.Syllabus;
using ShikshaMitra.Academic.Timetable.Settings.LectureSlots;
using ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.Academic.Calendar;
using ShikshaMitra.Academic.Timetable.RoomAvailability;
using ShikshaMitra.Academic.Timetable.Substitutions;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
    // ───────────────────────────────────────────────
    // Academic — Curriculum
    // ───────────────────────────────────────────────
    public DbSet<Subject> Subjects { get; set; }
    public DbSet<SubjectAuditLog> SubjectAuditLogs { get; set; }
    public DbSet<ElectiveGroup> ElectiveGroups { get; set; }
    public DbSet<AssignedSubject> StandardSubjectAssignments { get; set; }
    public DbSet<TeacherSubjectAllocation> TeacherSubjectAllocations { get; set; }
    public DbSet<StudentElectiveSelection> StudentElectiveSelections { get; set; }
    public DbSet<LessonPlan> LessonPlans { get; set; }
    public DbSet<TeacherDiary> TeacherDiaries { get; set; }

    // ───────────────────────────────────────────────
    // Academic — Class Teacher
    // ───────────────────────────────────────────────
    public DbSet<ClassTeacher> ClassTeachers { get; set; }

    // ───────────────────────────────────────────────
    // Academic — Syllabus
    // ───────────────────────────────────────────────
    public DbSet<AssignedSubjectChapter> SubjectChapters { get; set; }
    public DbSet<ChapterProgress> ChapterProgresses { get; set; }
    public DbSet<ChapterProgressLog> ChapterProgressLogs { get; set; }
    public DbSet<LearningResource> LearningResources { get; set; }

    // ───────────────────────────────────────────────
    // Academic — Homework & Assignments
    // ───────────────────────────────────────────────
    public DbSet<HomeworkAssignmentType> HomeworkAssignmentTypes { get; set; }
    public DbSet<HomeworkAssignmentEntity> HomeworkAssignments { get; set; }
    public DbSet<StudentSubmissionEntity> StudentSubmissions { get; set; }
    public DbSet<HomeworkType> HomeworkTypes { get; set; }
    public DbSet<AssignmentType> AssignmentTypes { get; set; }
    public DbSet<HomeworkDivisionSchedule> HomeworkDivisionSchedules { get; set; }
    public DbSet<AssignmentEntity> Assignments { get; set; }
    public DbSet<AssignmentDivisionSchedule> AssignmentDivisionSchedules { get; set; }
    public DbSet<AssessmentRubric> AssessmentRubrics { get; set; }
    public DbSet<AssessmentRubricCriterion> AssessmentRubricCriteria { get; set; }
    public DbSet<RubricEvaluation> RubricEvaluations { get; set; }
    public DbSet<RubricCriterionScore> RubricCriterionScores { get; set; }

    // ───────────────────────────────────────────────
    // Academic — Examination
    // ───────────────────────────────────────────────
    public DbSet<GradingScale> GradingScales { get; set; }
    public DbSet<ExamType> ExamTypes { get; set; }
    public DbSet<Exam> Exams { get; set; }
    public DbSet<ExamSubject> ExamSubjects { get; set; }
    public DbSet<ExamSubjectDivision> ExamSubjectDivisions { get; set; }
    public DbSet<ExamMark> ExamMarks { get; set; }
    public DbSet<ExamMarksWorkflowState> ExamMarksWorkflowStates { get; set; }
    public DbSet<ExamSeatAllocation> ExamSeatAllocations { get; set; }
    public DbSet<ExamSeatingPlan> ExamSeatingPlans { get; set; }
    public DbSet<ExamSeatingPlanSubject> ExamSeatingPlanSubjects { get; set; }
    public DbSet<ExamSeatingPublication> ExamSeatingPublications { get; set; }
    public DbSet<SeatingPlanAuditLog> SeatingPlanAuditLogs { get; set; }

    // Question Bank
    public DbSet<QuestionBankSetting> QuestionBankSettings { get; set; }
    public DbSet<QuestionBankItem> QuestionBankItems { get; set; }
    public DbSet<QuestionOption> QuestionOptions { get; set; }
    public DbSet<QuestionAttachment> QuestionAttachments { get; set; }
    public DbSet<QuestionTag> QuestionTags { get; set; }
    public DbSet<QuestionUsageLog> QuestionUsageLogs { get; set; }

    // Question Papers
    public DbSet<QuestionPaperSetting> QuestionPaperSettings { get; set; }
    public DbSet<QuestionPaper> QuestionPapers { get; set; }
    public DbSet<QuestionPaperSection> QuestionPaperSections { get; set; }
    public DbSet<QuestionPaperQuestion> QuestionPaperQuestions { get; set; }
    public DbSet<QuestionPaperBlueprint> QuestionPaperBlueprints { get; set; }
    public DbSet<QuestionPaperBlueprintChapterRule> QuestionPaperBlueprintChapterRules { get; set; }
    public DbSet<QuestionPaperBlueprintDifficultyRule> QuestionPaperBlueprintDifficultyRules { get; set; }
    public DbSet<QuestionPaperBlueprintBloomRule> QuestionPaperBlueprintBloomRules { get; set; }

    // Result lifecycle
    public DbSet<ExamResultHeader> ExamResultHeaders { get; set; }
    public DbSet<ExamResultDetail> ExamResultDetails { get; set; }
    public DbSet<ExamResultAuditLog> ExamResultAuditLogs { get; set; }

    // ───────────────────────────────────────────────
    // Academic — Timetable (Settings)
    // ───────────────────────────────────────────────
    public DbSet<WeeklyLectureSlot> WeeklyLectureSlots { get; set; }
    public DbSet<WeeklyLectureSlotSubject> WeeklyLectureSlotSubjects { get; set; }
    public DbSet<LectureSlotConfig> LectureSlotConfigs { get; set; }
    public DbSet<LectureSlotGroup> LectureSlotGroups { get; set; }
    public DbSet<LectureSlotTimeSlot> LectureSlotTimeSlots { get; set; }

    // ───────────────────────────────────────────────
    // Academic — Timetable
    // ───────────────────────────────────────────────
    public DbSet<Timetable> Timetables { get; set; }
    public DbSet<TimetableSlot> TimetableSlots { get; set; }
    public DbSet<TimetableSlotSubject> TimetableSlotSubjects { get; set; }

    // ───────────────────────────────────────────────
    // Academic — Room Availability
    // ───────────────────────────────────────────────
    public DbSet<RoomReservation> RoomReservations { get; set; }
    public DbSet<RoomRecurringConfig> RoomRecurringConfigs { get; set; }
    public DbSet<RoomMaintenanceBlock> RoomMaintenanceBlocks { get; set; }
    public DbSet<RoomConfig> RoomConfigs { get; set; }
    public DbSet<RoomAuditLog> RoomAuditLogs { get; set; }
    public DbSet<SubstitutionRequest> AcademicSubstitutionRequests { get; set; }
    public DbSet<TeacherAvailability> AcademicTeacherAvailabilities { get; set; }
    public DbSet<SubstitutionAuditLog> AcademicSubstitutionAuditLogs { get; set; }
    public DbSet<SubstitutionNotificationLog> AcademicSubstitutionNotificationLogs { get; set; }

    // ───────────────────────────────────────────────
    // Academic — Calendar
    // ───────────────────────────────────────────────
    public DbSet<AcademicCalendar> AcademicCalendars { get; set; }
    public DbSet<AcademicCalendarDay> AcademicCalendarDays { get; set; }
    public DbSet<AcademicCalendarWorkingHour> AcademicCalendarWorkingHours { get; set; }
    public DbSet<AcademicCalendarSetting> AcademicCalendarSettings { get; set; }
    public DbSet<CalendarImpactLog> CalendarImpactLogs { get; set; }
    public DbSet<AcademicCalendarAuditLog> AcademicCalendarAuditLogs { get; set; }
    public DbSet<AcademicCalendarAuditReviewNote> AcademicCalendarAuditReviewNotes { get; set; }
}

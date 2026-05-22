using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.SeatAllocation;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.Academic.Homework;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Academic.Syllabus;
using ShikshaMitra.Academic.Timetable.Settings.LectureSlots;
using ShikshaMitra.Academic.Timetable.Settings.WeeklyLectureSlots;
using ShikshaMitra.Academic.Timetable.Timetables;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
    // ───────────────────────────────────────────────
    // Academic — Curriculum
    // ───────────────────────────────────────────────
    public DbSet<Subject> Subjects { get; set; }
    public DbSet<ElectiveGroup> ElectiveGroups { get; set; }
    public DbSet<AssignedSubject> StandardSubjectAssignments { get; set; }
    public DbSet<TeacherSubjectAllocation> TeacherSubjectAllocations { get; set; }
    public DbSet<StudentElectiveSelection> StudentElectiveSelections { get; set; }

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

    // ───────────────────────────────────────────────
    // Academic — Examination
    // ───────────────────────────────────────────────
    public DbSet<GradingScale> GradingScales { get; set; }
    public DbSet<ExamType> ExamTypes { get; set; }
    public DbSet<Exam> Exams { get; set; }
    public DbSet<ExamMark> ExamMarks { get; set; }
    public DbSet<ExamSeatAllocation> ExamSeatAllocations { get; set; }

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
}

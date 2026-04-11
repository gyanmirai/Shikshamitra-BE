using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.Academic.Homework;
using ShikshaMitra.Academic.HomeworkAssignment;

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

    // ───────────────────────────────────────────────
    // Academic — Class Teacher
    // ───────────────────────────────────────────────
    public DbSet<ClassTeacher> ClassTeachers { get; set; }

    // ───────────────────────────────────────────────
    // Academic — Syllabus
    // ───────────────────────────────────────────────
    public DbSet<AssignedSubjectChapter> SubjectChapters { get; set; }

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
}

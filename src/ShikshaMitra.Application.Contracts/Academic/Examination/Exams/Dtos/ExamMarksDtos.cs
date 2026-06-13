using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.Examination.Exams.Dtos;

// ─── GET /api/academics/exams/marks ───

public class GetExamMarksInput
{
    [Required]
    public Guid ExamId { get; set; }

    [Required]
    public Guid SubjectId { get; set; }

    [Required]
    public Guid DivisionId { get; set; }

    public Guid? StandardId { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(10, 100)]
    public int PageSize { get; set; } = 20;

    public string? Search { get; set; }
}

public class GetExamMarksResponseDto
{
    public Guid ExamId { get; set; }

    // ─── Workflow state ──────────────────────────────────────────────────────
    /// <summary>
    /// Current marks-entry workflow status.
    /// 0 = Draft | 1 = Submitted | 2 = Verified | 3 = Published (locked)
    /// </summary>
    public int WorkflowStatus { get; set; }

    public bool IsDraft { get; set; }
    public bool IsSubmitted { get; set; }
    public bool IsVerified { get; set; }
    public bool IsPublished { get; set; }

    // ─── Legacy mark-publish flag (per-student publish via old flow) ─────────
    public bool IsMarksPublish { get; set; }

    // ─── Pagination ──────────────────────────────────────────────────────────
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public List<StudentExamMarksItemDto> Items { get; set; } = new();
}

public class StudentExamMarksItemDto
{
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string? GrNo { get; set; }
    public string? RollNo { get; set; }
    public double? ObtainedMarks { get; set; }
    public double? TotalMarks { get; set; }
    public double? PassingMarks { get; set; }
    public string? Grade { get; set; }
    public string? Feedback { get; set; }

    /// <summary>0 = Pending, 1 = Assigned</summary>
    public int Status { get; set; }

    /// <summary>0 = Present | 1 = Absent | 2 = MedicalLeave | 3 = Exempted</summary>
    public int AttendanceStatus { get; set; }
}

// ─── PATCH /api/academics/exams/{examId}/students/{studentId}/marks ───

public class UpdateStudentMarksDto
{
    /// <summary>
    /// Required when AttendanceStatus is Present (0). Leave null for absent/exempted students.
    /// </summary>
    [Range(0, double.MaxValue)]
    public double? Marks { get; set; }

    [Required]
    [Range(1, 10000)]
    public double TotalMarks { get; set; }

    [MaxLength(1000)]
    public string? Feedback { get; set; }

    /// <summary>0 = Present | 1 = Absent | 2 = MedicalLeave | 3 = Exempted. Defaults to Present.</summary>
    public int AttendanceStatus { get; set; } = 0;
}

public class UpdateStudentMarksResponseDto
{
    public string Message { get; set; } = string.Empty;
    public double? Marks { get; set; }
    public double TotalMarks { get; set; }
    public string? Grade { get; set; }
    public string? Feedback { get; set; }
    /// <summary>0 = Present | 1 = Absent | 2 = MedicalLeave | 3 = Exempted</summary>
    public int AttendanceStatus { get; set; }
}

// ─── PATCH /api/academics/exams/{examId}/publish-marks ───

public class PublishMarksResponseDto
{
    public string Message { get; set; } = string.Empty;
    public Guid ExamId { get; set; }
    public Guid DivisionId { get; set; }
    public DateTime PublishedAt { get; set; }
    public int StudentsCount { get; set; }
}

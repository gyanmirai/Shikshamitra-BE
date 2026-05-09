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
    public bool IsMarksPublish { get; set; }
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
    public string? Grade { get; set; }
    public string? Feedback { get; set; }

    /// <summary>0 = Pending, 1 = Assigned</summary>
    public int Status { get; set; }
}

// ─── PATCH /api/academics/exams/{examId}/students/{studentId}/marks ───

public class UpdateStudentMarksDto
{
    [Required]
    [Range(0, double.MaxValue)]
    public double Marks { get; set; }

    [Required]
    [Range(1, 10000)]
    public double TotalMarks { get; set; }

    [MaxLength(1000)]
    public string? Feedback { get; set; }
}

public class UpdateStudentMarksResponseDto
{
    public string Message { get; set; } = string.Empty;
    public double Marks { get; set; }
    public double TotalMarks { get; set; }
    public string? Grade { get; set; }
    public string? Feedback { get; set; }
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

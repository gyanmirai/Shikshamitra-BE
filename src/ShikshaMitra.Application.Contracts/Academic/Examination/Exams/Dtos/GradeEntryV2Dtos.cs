using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Academic.Examination.Exams.Dtos;

public class BulkSaveMarksInputDto
{
    [Required]
    public Guid SubjectId { get; set; }

    [Required]
    public Guid DivisionId { get; set; }

    [Required]
    public List<StudentMarkRowInputDto> Rows { get; set; } = new();
}

public class StudentMarkRowInputDto
{
    [Required]
    public Guid StudentId { get; set; }

    /// <summary>0 = Present | 1 = Absent | 2 = MedicalLeave | 3 = Exempted</summary>
    [Range(0, 3)]
    public int AttendanceStatus { get; set; }

    [Range(0, 10000)]
    public double? ObtainedMarks { get; set; }

    [MaxLength(1000)]
    public string? Feedback { get; set; }
}

public class MarksValidationResponseDto
{
    public bool IsValid { get; set; }
    public List<MarksValidationErrorDto> Blockers { get; set; } = new();
    public List<MarksValidationErrorDto> Warnings { get; set; } = new();
}

public class MarksValidationErrorDto
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? StudentId { get; set; }
    public string? StudentName { get; set; }
}

public class RequestChangesInputDto
{
    [Required]
    public Guid SubjectId { get; set; }

    [Required]
    public Guid DivisionId { get; set; }

    [MaxLength(2000)]
    public string? ReviewerNote { get; set; }
}

public class UnlockWorkflowInputDto
{
    [Required]
    public Guid SubjectId { get; set; }

    [Required]
    public Guid DivisionId { get; set; }

    [Required]
    [MinLength(5)]
    [MaxLength(2000)]
    public string Reason { get; set; } = string.Empty;
}

public class GradeEntryAuditLogDto
{
    public DateTime Timestamp { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ChangedBy { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}

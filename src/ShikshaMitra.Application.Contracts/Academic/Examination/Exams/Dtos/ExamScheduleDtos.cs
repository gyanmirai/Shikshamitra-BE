using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ShikshaMitra.Common;

namespace ShikshaMitra.Academic.Examination.Exams.Dtos;

public class ExamScheduleRequestDto
{
    [Required]
    [MinLength(1)]
    public List<ExamSubjectScheduleDto> Subjects { get; set; } = new();
}

public class ExamSubjectScheduleDto
{
    [Required]
    public Guid SubjectId { get; set; }

    public string? Notes { get; set; }

    public DateTime? ExamDate { get; set; }

    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public double? TotalMarks { get; set; }

    public double? PassingMarks { get; set; }
}

public class ExamScheduleResponseDto : ExamScheduleRequestDto
{
}

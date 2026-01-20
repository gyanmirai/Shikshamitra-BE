using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ShikshaMitra.Common;

namespace ShikshaMitra.Academic.Examination.Exams.Dtos;

public class ExamRoomAssignmentRequestDto
{
    [Required]
    public bool IsRoomAssigned { get; set; }

    public List<ExamRoomSubjectDto> Subjects { get; set; } = new();
}

public class ExamRoomSubjectDto
{
    [Required]
    public Guid SubjectId { get; set; }

    public DateTime? ExamDate { get; set; }

    [Required]
    [MinLength(1)]
    public List<ExamRoomDivisionDto> Divisions { get; set; } = new();
}

public class ExamRoomDivisionDto
{
    [Required]
    public Guid DivisionId { get; set; }

    [Required]
    [MinLength(1)]
    public List<Guid> RoomIds { get; set; } = new();

    [Required]
    public int Capacity { get; set; }
}

public class ExamRoomAssignmentResponseDto : ExamRoomAssignmentRequestDto
{
}

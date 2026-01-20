using System.Collections.Generic;

namespace ShikshaMitra.Academic.Examination.Quizzes.Dtos;

public class QuizStudentsResponseDto
{
    public List<QuizStudentResultItemDto> Items { get; set; } = new();
    public long TotalCount { get; set; }
    public int TotalPages { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}

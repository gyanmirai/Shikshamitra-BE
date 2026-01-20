using System.Collections.Generic;

namespace ShikshaMitra.Academic.Examination.Quizzes.Dtos;

public class QuizStep2RequestDto
{
    public List<QuizQuestionInputDto> Questions { get; set; } = new();
    public bool IsRandomOrder { get; set; }
}

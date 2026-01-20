using System.Collections.Generic;

namespace ShikshaMitra.Academic.Examination.Quizzes.Dtos;

public class QuizMarksRequestDto
{
    public List<QuizQuestionMarkInputDto> Questions { get; set; } = new();
}

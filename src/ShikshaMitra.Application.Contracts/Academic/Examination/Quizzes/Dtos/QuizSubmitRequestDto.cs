using System.Collections.Generic;

namespace ShikshaMitra.Academic.Examination.Quizzes.Dtos;

public class QuizSubmitRequestDto
{
    public List<QuizAnswerInputDto> Answers { get; set; } = new();
}

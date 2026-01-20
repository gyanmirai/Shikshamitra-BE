using System.Collections.Generic;

namespace ShikshaMitra.Academic.Examination.Quizzes.Dtos;

public class QuizStep2ResponseDto
{
    public bool IsRandomOrder { get; set; }
    public List<QuizQuestionResponseDto> Questions { get; set; } = new();
}

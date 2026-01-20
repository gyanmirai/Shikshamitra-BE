using System.Collections.Generic;

namespace ShikshaMitra.Academic.Examination.Quizzes.Dtos;

public class QuizSubmissionResponseDto
{
    public List<QuizSubmissionQuestionDto> Questions { get; set; } = new();
}

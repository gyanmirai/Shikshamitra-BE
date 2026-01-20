using System;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Quizzes.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Examination.Quizzes;

public interface IQuizAppService : IApplicationService
{
    Task<QuizCreateResponseDto> CreateStep1Async(string academicMediumId, QuizStep1RequestDto input);
    Task UpdateQuestionsAsync(Guid quizId, QuizStep2RequestDto input);
    Task<PagedResultDto<QuizListDto>> GetListAsync(string academicMediumId, QuizStatus status, int page, int pageSize);
    Task<QuizStep1ResponseDto> GetStep1Async(Guid quizId, string academicMediumId);
    Task<QuizStep2ResponseDto> GetStep2Async(Guid quizId);
    Task<QuizStudentsResponseDto> GetStudentsResultAsync(Guid quizId, int page, int pageSize);
    Task<QuizSubmissionResponseDto> GetStudentSubmissionAsync(Guid quizId, Guid studentId);
    Task SubmitQuizAsync(Guid quizId, Guid studentId, QuizSubmitRequestDto input);
    Task SubmitMarksAsync(Guid quizId, Guid studentId, QuizMarksRequestDto input);
}

public class QuizStep1ResponseDto : QuizDto
{
    // Reusing QuizDto structure for Step 1 response as it matches the fields
}

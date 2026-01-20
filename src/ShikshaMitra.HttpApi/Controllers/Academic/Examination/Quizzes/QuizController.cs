using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Examination.Quizzes;
using ShikshaMitra.Academic.Examination.Quizzes.Dtos;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Controllers.Academic.Examination.Quizzes;

[RemoteService(Name = "ShikshaMitra")]
public class QuizController : ShikshaMitraController, IQuizAppService
{
    private readonly IQuizAppService _quizAppService;

    public QuizController(IQuizAppService quizAppService)
    {
        _quizAppService = quizAppService;
    }

    [HttpPost("quizzes")]
    public Task<QuizCreateResponseDto> CreateStep1Async(string academicMediumId, QuizStep1RequestDto input)
    {
        return _quizAppService.CreateStep1Async(academicMediumId, input);
    }

    [HttpGet("quizzes")]
    public Task<PagedResultDto<QuizListDto>> GetListAsync(string academicMediumId, QuizStatus status, int page, int pageSize)
    {
        return _quizAppService.GetListAsync(academicMediumId, status, page, pageSize);
    }

    [HttpPut("quizzes/{quizId}")]
    public Task UpdateStep1Async(Guid quizId, string academicMediumId, QuizStep1RequestDto input)
    {
        return _quizAppService.UpdateStep1Async(quizId, academicMediumId, input);
    }

    [HttpGet("quizzes/{quizId}")]
    public Task<QuizStep1ResponseDto> GetStep1Async(Guid quizId, string academicMediumId)
    {
        return _quizAppService.GetStep1Async(quizId, academicMediumId);
    }

    [HttpGet("quizzes/{quizId}/questions")]
    public Task<QuizStep2ResponseDto> GetStep2Async(Guid quizId)
    {
        return _quizAppService.GetStep2Async(quizId);
    }

    [HttpPut("quizzes/{quizId}/questions")]
    public Task UpdateQuestionsAsync(Guid quizId, QuizStep2RequestDto input)
    {
        return _quizAppService.UpdateQuestionsAsync(quizId, input);
    }

    [HttpGet("quizzes/{quizId}/students")]
    public Task<QuizStudentsResponseDto> GetStudentsResultAsync(Guid quizId, int page, int pageSize)
    {
        return _quizAppService.GetStudentsResultAsync(quizId, page, pageSize);
    }

    [HttpGet("quizzes/{quizId}/submissions/{studentId}")]
    public Task<QuizSubmissionResponseDto> GetStudentSubmissionAsync(Guid quizId, Guid studentId)
    {
        return _quizAppService.GetStudentSubmissionAsync(quizId, studentId);
    }

    [HttpPost("quizzes/{quizId}/submit")]
    public Task SubmitQuizAsync(Guid quizId, Guid studentId, QuizSubmitRequestDto input)
    {
        return _quizAppService.SubmitQuizAsync(quizId, studentId, input);
    }

    [HttpPatch("quizzes/{quizId}/submissions/{studentId}/marks")]
    public Task SubmitMarksAsync(Guid quizId, Guid studentId, QuizMarksRequestDto input)
    {
        return _quizAppService.SubmitMarksAsync(quizId, studentId, input);
    }
}

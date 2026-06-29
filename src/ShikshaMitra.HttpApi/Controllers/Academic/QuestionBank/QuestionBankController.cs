using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.QuestionBank;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.QuestionBank;

[ApiController]
[Authorize]
[Route("api/academics/question-bank")]
public class QuestionBankController(IQuestionBankAppService service) : AbpControllerBase
{
    [HttpGet("settings")]
    public Task<QuestionBankSettingDto> GetSettings([FromQuery] Guid academicYearId) => service.GetSettingsAsync(academicYearId);

    [HttpPost("settings")]
    public Task<QuestionBankSettingDto> SaveSettings([FromBody] QuestionBankSettingDto input) => service.SaveSettingsAsync(input);

    [HttpPut("settings/{id:guid}")]
    public Task<QuestionBankSettingDto> UpdateSettings(Guid id, [FromBody] QuestionBankSettingDto input)
    {
        input.Id = id;
        return service.SaveSettingsAsync(input);
    }

    [HttpGet("dashboard")]
    public Task<QuestionBankDashboardDto> GetDashboard([FromQuery] Guid academicYearId) => service.GetDashboardAsync(academicYearId);

    [HttpGet("questions")]
    public Task<QuestionListResponseDto> GetQuestions([FromQuery] QuestionSearchInput input) => service.GetQuestionsAsync(input);

    [HttpGet("questions/search")]
    public Task<QuestionListResponseDto> SearchQuestions([FromQuery] QuestionSearchInput input) => service.GetQuestionsAsync(input);

    [HttpGet("questions/{id:guid}")]
    public Task<QuestionBankItemDto> GetQuestion(Guid id) => service.GetQuestionAsync(id);

    [HttpPost("questions")]
    public Task<QuestionBankItemDto> CreateQuestion([FromBody] CreateUpdateQuestionDto input) => service.CreateQuestionAsync(input);

    [HttpPut("questions/{id:guid}")]
    public Task<QuestionBankItemDto> UpdateQuestion(Guid id, [FromBody] CreateUpdateQuestionDto input) => service.UpdateQuestionAsync(id, input);

    [HttpDelete("questions/{id:guid}")]
    public async Task<IActionResult> DeleteQuestion(Guid id)
    {
        await service.DeleteQuestionAsync(id);
        return NoContent();
    }

    [HttpPost("questions/{id:guid}/submit")]
    public Task<QuestionBankItemDto> SubmitQuestion(Guid id) => service.SubmitQuestionAsync(id);

    [HttpPost("questions/{id:guid}/approve")]
    public Task<QuestionBankItemDto> ApproveQuestion(Guid id, [FromBody] QuestionReviewDto input) => service.ApproveQuestionAsync(id, input);

    [HttpPost("questions/{id:guid}/reject")]
    public Task<QuestionBankItemDto> RejectQuestion(Guid id, [FromBody] QuestionReviewDto input) => service.RejectQuestionAsync(id, input);

    [HttpPost("questions/{id:guid}/archive")]
    public Task<QuestionBankItemDto> ArchiveQuestion(Guid id) => service.ArchiveQuestionAsync(id);

    [HttpPost("questions/{id:guid}/duplicate")]
    public Task<QuestionBankItemDto> DuplicateQuestion(Guid id) => service.DuplicateQuestionAsync(id);

    [HttpPost("questions/check-duplicate")]
    public Task<DuplicateQuestionResultDto> CheckDuplicate([FromBody] CheckDuplicateQuestionDto input) => service.CheckDuplicateAsync(input);

    [HttpPost("questions/bulk-approve")]
    public Task<BulkQuestionWorkflowResultDto> BulkApprove([FromBody] BulkQuestionReviewDto input) => service.BulkApproveAsync(input);

    [HttpPost("questions/bulk-reject")]
    public Task<BulkQuestionWorkflowResultDto> BulkReject([FromBody] BulkQuestionReviewDto input) => service.BulkRejectAsync(input);
}

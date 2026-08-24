using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.QuestionBank;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.QuestionBank;

[ApiController]
[Authorize]
[Route("api/academics/question-bank")]
public class QuestionBankController(IQuestionBankAppService service) : AbpControllerBase
{
    [HttpGet("settings")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionBank.View)]
    public Task<QuestionBankSettingDto> GetSettings([FromQuery] Guid academicYearId) => service.GetSettingsAsync(academicYearId);

    [HttpPost("settings")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionBank.Create)]
    public Task<QuestionBankSettingDto> SaveSettings([FromBody] QuestionBankSettingDto input) => service.SaveSettingsAsync(input);

    [HttpPut("settings/{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionBank.Edit)]
    public Task<QuestionBankSettingDto> UpdateSettings(Guid id, [FromBody] QuestionBankSettingDto input)
    {
        input.Id = id;
        return service.SaveSettingsAsync(input);
    }

    [HttpGet("dashboard")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionBank.View)]
    public Task<QuestionBankDashboardDto> GetDashboard([FromQuery] Guid academicYearId) => service.GetDashboardAsync(academicYearId);

    [HttpGet("questions/bulk-template")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionBank.View)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadBulkTemplate()
    {
        var template = await service.GetBulkTemplateAsync();
        return File(template.Content, template.ContentType, template.FileName);
    }

    [HttpGet("questions")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionBank.View)]
    public Task<QuestionListResponseDto> GetQuestions([FromQuery] QuestionSearchInput input) => service.GetQuestionsAsync(input);

    [HttpGet("questions/search")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionBank.View)]
    public Task<QuestionListResponseDto> SearchQuestions([FromQuery] QuestionSearchInput input) => service.GetQuestionsAsync(input);

    [HttpGet("questions/{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionBank.View)]
    public Task<QuestionBankItemDto> GetQuestion(Guid id) => service.GetQuestionAsync(id);

    [HttpPost("questions")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionBank.Create)]
    public Task<QuestionBankItemDto> CreateQuestion([FromBody] CreateUpdateQuestionDto input) => service.CreateQuestionAsync(input);

    [HttpPut("questions/{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionBank.Edit)]
    public Task<QuestionBankItemDto> UpdateQuestion(Guid id, [FromBody] CreateUpdateQuestionDto input) => service.UpdateQuestionAsync(id, input);

    [HttpDelete("questions/{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionBank.Delete)]
    public async Task<IActionResult> DeleteQuestion(Guid id)
    {
        await service.DeleteQuestionAsync(id);
        return NoContent();
    }

    [HttpPost("questions/{id:guid}/submit")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionBank.Submit)]
    public Task<QuestionBankItemDto> SubmitQuestion(Guid id) => service.SubmitQuestionAsync(id);

    [HttpPost("questions/{id:guid}/approve")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionBank.Approve)]
    public Task<QuestionBankItemDto> ApproveQuestion(Guid id, [FromBody] QuestionReviewDto input) => service.ApproveQuestionAsync(id, input);

    [HttpPost("questions/{id:guid}/reject")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionBank.Reject)]
    public Task<QuestionBankItemDto> RejectQuestion(Guid id, [FromBody] QuestionReviewDto input) => service.RejectQuestionAsync(id, input);

    [HttpPost("questions/{id:guid}/archive")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionBank.Archive)]
    public Task<QuestionBankItemDto> ArchiveQuestion(Guid id) => service.ArchiveQuestionAsync(id);

    [HttpPost("questions/{id:guid}/duplicate")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionBank.Duplicate)]
    public Task<QuestionBankItemDto> DuplicateQuestion(Guid id) => service.DuplicateQuestionAsync(id);

    [HttpPost("questions/check-duplicate")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionBank.View)]
    public Task<DuplicateQuestionResultDto> CheckDuplicate([FromBody] CheckDuplicateQuestionDto input) => service.CheckDuplicateAsync(input);

    [HttpPost("questions/bulk-approve")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionBank.Approve)]
    public Task<BulkQuestionWorkflowResultDto> BulkApprove([FromBody] BulkQuestionReviewDto input) => service.BulkApproveAsync(input);

    [HttpPost("questions/bulk-reject")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionBank.Reject)]
    public Task<BulkQuestionWorkflowResultDto> BulkReject([FromBody] BulkQuestionReviewDto input) => service.BulkRejectAsync(input);
}

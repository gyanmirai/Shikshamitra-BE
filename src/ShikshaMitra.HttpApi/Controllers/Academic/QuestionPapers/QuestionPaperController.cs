using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.QuestionPapers;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.QuestionPapers;

[ApiController]
[Authorize]
[Route("api/academics/question-papers")]
public class QuestionPaperController(IQuestionPaperAppService service) : AbpControllerBase
{
    [HttpGet("settings")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionPapers.View)]
    public Task<QuestionPaperSettingDto> GetSettings([FromQuery] Guid academicYearId) => service.GetSettingsAsync(academicYearId);

    [HttpPost("settings")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionPapers.Add)]
    public Task<QuestionPaperSettingDto> SaveSettings([FromBody] QuestionPaperSettingDto input) => service.SaveSettingsAsync(input);

    [HttpPut("settings/{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionPapers.Add)]
    public Task<QuestionPaperSettingDto> UpdateSettings(Guid id, [FromBody] QuestionPaperSettingDto input)
    {
        input.Id = id;
        return service.SaveSettingsAsync(input);
    }

    [HttpGet("dashboard")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionPapers.View)]
    public Task<QuestionPaperDashboardDto> GetDashboard([FromQuery] Guid academicYearId) => service.GetDashboardAsync(academicYearId);

    [HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionPapers.View)]
    public Task<QuestionPaperListResponseDto> GetPapers([FromQuery] QuestionPaperSearchInput input) => service.GetPapersAsync(input);

    [HttpGet("{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionPapers.View)]
    public Task<QuestionPaperDto> GetPaper(Guid id) => service.GetPaperAsync(id);

    [HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionPapers.Add)]
    public Task<QuestionPaperDto> CreatePaper([FromBody] CreateUpdateQuestionPaperDto input) => service.CreatePaperAsync(input);

    [HttpPut("{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionPapers.Add)]
    public Task<QuestionPaperDto> UpdatePaper(Guid id, [FromBody] CreateUpdateQuestionPaperDto input) => service.UpdatePaperAsync(id, input);

    [HttpDelete("{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionPapers.Delete)]
    public async Task<IActionResult> DeletePaper(Guid id)
    {
        await service.DeletePaperAsync(id);
        return NoContent();
    }

    [HttpPost("{id:guid}/submit")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionPapers.Add)]
    public Task<QuestionPaperDto> Submit(Guid id) => service.SubmitAsync(id);

    [HttpPost("{id:guid}/approve")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionPapers.Add)]
    public Task<QuestionPaperDto> Approve(Guid id, [FromBody] PaperReviewDto input) => service.ApproveAsync(id, input);

    [HttpPost("{id:guid}/reject")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionPapers.Add)]
    public Task<QuestionPaperDto> Reject(Guid id, [FromBody] PaperReviewDto input) => service.RejectAsync(id, input);

    [HttpPost("{id:guid}/publish")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionPapers.Add)]
    public Task<QuestionPaperDto> Publish(Guid id) => service.PublishAsync(id);

    [HttpPost("{id:guid}/lock")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionPapers.Add)]
    public Task<QuestionPaperDto> Lock(Guid id) => service.LockAsync(id);

    [HttpPost("{id:guid}/archive")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionPapers.Delete)]
    public Task<QuestionPaperDto> Archive(Guid id) => service.ArchiveAsync(id);

    [HttpGet("{paperId:guid}/blueprint")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionPapers.View)]
    public Task<QuestionPaperBlueprintDto?> GetBlueprint(Guid paperId) => service.GetBlueprintAsync(paperId);

    [HttpPost("{paperId:guid}/blueprint")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionPapers.Add)]
    public Task<QuestionPaperBlueprintDto> SaveBlueprint(Guid paperId, [FromBody] QuestionPaperBlueprintDto input) => service.SaveBlueprintAsync(paperId, input);

    [HttpPut("{paperId:guid}/blueprint")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionPapers.Add)]
    public Task<QuestionPaperBlueprintDto> UpdateBlueprint(Guid paperId, [FromBody] QuestionPaperBlueprintDto input) => service.SaveBlueprintAsync(paperId, input);

    [HttpPost("{paperId:guid}/blueprint/validate")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionPapers.View)]
    public Task<BlueprintValidationResultDto> ValidateBlueprint(Guid paperId) => service.ValidateBlueprintAsync(paperId);

    [HttpPost("{paperId:guid}/generate")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionPapers.Add)]
    public Task<QuestionPaperDto> Generate(Guid paperId) => service.GenerateAsync(paperId);

    [HttpPost("{paperId:guid}/regenerate")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionPapers.Add)]
    public Task<QuestionPaperDto> Regenerate(Guid paperId) => service.GenerateAsync(paperId);

    [HttpPost("{paperId:guid}/add-question")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionPapers.Add)]
    public Task<QuestionPaperDto> AddQuestion(Guid paperId, [FromBody] AddPaperQuestionDto input) => service.AddQuestionAsync(paperId, input);

    [HttpPost("{paperId:guid}/remove-question")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionPapers.Add)]
    public Task<QuestionPaperDto> RemoveQuestion(Guid paperId, [FromBody] Guid paperQuestionId) => service.RemoveQuestionAsync(paperId, paperQuestionId);

    [HttpPost("{paperId:guid}/reorder-questions")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionPapers.Add)]
    public Task<QuestionPaperDto> ReorderQuestions(Guid paperId, [FromBody] ReorderPaperQuestionsDto input) => service.ReorderQuestionsAsync(paperId, input);

    [HttpGet("{paperId:guid}/preview")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.Examination_QuestionPapers.View)]
    public Task<QuestionPaperPreviewDto> GetPreview(Guid paperId) => service.GetPreviewAsync(paperId);
}

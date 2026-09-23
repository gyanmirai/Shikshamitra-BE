using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.HomeworkAssignment;

[ApiController]
[Authorize]
[Route("api/academic/rubrics")]
public class RubricAssessmentController(IRubricAssessmentAppService service) : AbpControllerBase
{
	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_SettingsRubricConfig.Add)]
	[ProducesResponseType(typeof(AssessmentRubricDto), 200)]
	public Task<AssessmentRubricDto> CreateAsync([FromBody] CreateUpdateRubricDto input)
	{
		return service.CreateAsync(input);
	}

	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_SettingsRubricConfig.View)]
	[ProducesResponseType(typeof(List<AssessmentRubricDto>), 200)]
	public Task<List<AssessmentRubricDto>> GetListAsync([FromQuery] GetRubricsInput input)
	{
		return service.GetListAsync(input);
	}

	[HttpGet("{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_SettingsRubricConfig.View)]
	[ProducesResponseType(typeof(AssessmentRubricDto), 200)]
	public Task<AssessmentRubricDto> GetAsync(Guid id)
	{
		return service.GetAsync(id);
	}

	[HttpPut("{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_SettingsRubricConfig.Add)]
	[ProducesResponseType(typeof(AssessmentRubricDto), 200)]
	public Task<AssessmentRubricDto> UpdateAsync(Guid id, [FromBody] CreateUpdateRubricDto input)
	{
		return service.UpdateAsync(id, input);
	}

	[HttpPost("{id:guid}/copy-to-task")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.Add)]
	[ProducesResponseType(typeof(AssessmentRubricDto), 200)]
	public Task<AssessmentRubricDto> CreateFromTemplateAsync(
		Guid id,
		[FromBody] CreateRubricFromTemplateDto input)
	{
		return service.CreateFromTemplateAsync(id, input);
	}

	[HttpPost("{id:guid}/publish")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_SettingsRubricConfig.Add)]
	public Task PublishAsync(Guid id)
	{
		return service.PublishAsync(id);
	}

	[HttpPost("{id:guid}/archive")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_SettingsRubricConfig.Delete)]
	public Task ArchiveAsync(Guid id)
	{
		return service.ArchiveAsync(id);
	}

	[HttpPost("{id:guid}/evaluations")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_AssessmentAndFeedback.Add)]
	[ProducesResponseType(typeof(RubricEvaluationDto), 200)]
	public Task<RubricEvaluationDto> UpsertEvaluationAsync(
		Guid id,
		[FromBody] UpsertRubricEvaluationDto input)
	{
		return service.UpsertEvaluationAsync(id, input);
	}

	[HttpGet("{id:guid}/evaluations")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_AssessmentAndFeedback.View)]
	[ProducesResponseType(typeof(List<RubricEvaluationDto>), 200)]
	public Task<List<RubricEvaluationDto>> GetEvaluationsAsync(Guid id)
	{
		return service.GetEvaluationsAsync(id);
	}

	[HttpGet("evaluations/student/{studentId:guid}/assignment/{assignmentId:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_AssessmentAndFeedback.View)]
	[ProducesResponseType(typeof(RubricEvaluationDto), 200)]
	[ProducesResponseType(204)]
	public Task<RubricEvaluationDto?> GetStudentAssignmentEvaluationAsync(Guid studentId, Guid assignmentId)
	{
		return service.GetStudentAssignmentEvaluationAsync(studentId, assignmentId);
	}

	[HttpPost("evaluations/{evaluationId:guid}/submit")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_AssessmentAndFeedback.Add)]
	public Task SubmitEvaluationAsync(Guid evaluationId)
	{
		return service.SubmitEvaluationAsync(evaluationId);
	}

	[HttpPost("evaluations/{evaluationId:guid}/approve")]
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_AssessmentAndFeedback.Add)]
	public Task ApproveEvaluationAsync(Guid evaluationId)
	{
		return service.ApproveEvaluationAsync(evaluationId);
	}
}

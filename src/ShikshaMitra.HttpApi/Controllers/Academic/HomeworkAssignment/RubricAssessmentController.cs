using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.HomeworkAssignment;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.HomeworkAssignment;

[ApiController]
[Authorize]
[Route("api/academic/rubrics")]
public class RubricAssessmentController(IRubricAssessmentAppService service) : AbpControllerBase
{
	[HttpPost]
	[ProducesResponseType(typeof(AssessmentRubricDto), 200)]
	public Task<AssessmentRubricDto> CreateAsync([FromBody] CreateUpdateRubricDto input)
	{
		return service.CreateAsync(input);
	}

	[HttpGet]
	[ProducesResponseType(typeof(List<AssessmentRubricDto>), 200)]
	public Task<List<AssessmentRubricDto>> GetListAsync([FromQuery] GetRubricsInput input)
	{
		return service.GetListAsync(input);
	}

	[HttpGet("{id:guid}")]
	[ProducesResponseType(typeof(AssessmentRubricDto), 200)]
	public Task<AssessmentRubricDto> GetAsync(Guid id)
	{
		return service.GetAsync(id);
	}

	[HttpPut("{id:guid}")]
	[ProducesResponseType(typeof(AssessmentRubricDto), 200)]
	public Task<AssessmentRubricDto> UpdateAsync(Guid id, [FromBody] CreateUpdateRubricDto input)
	{
		return service.UpdateAsync(id, input);
	}

	[HttpPost("{id:guid}/publish")]
	public Task PublishAsync(Guid id)
	{
		return service.PublishAsync(id);
	}

	[HttpPost("{id:guid}/archive")]
	public Task ArchiveAsync(Guid id)
	{
		return service.ArchiveAsync(id);
	}

	[HttpPost("{id:guid}/evaluations")]
	[ProducesResponseType(typeof(RubricEvaluationDto), 200)]
	public Task<RubricEvaluationDto> UpsertEvaluationAsync(
		Guid id,
		[FromBody] UpsertRubricEvaluationDto input)
	{
		return service.UpsertEvaluationAsync(id, input);
	}

	[HttpGet("{id:guid}/evaluations")]
	[ProducesResponseType(typeof(List<RubricEvaluationDto>), 200)]
	public Task<List<RubricEvaluationDto>> GetEvaluationsAsync(Guid id)
	{
		return service.GetEvaluationsAsync(id);
	}

	[HttpPost("evaluations/{evaluationId:guid}/submit")]
	public Task SubmitEvaluationAsync(Guid evaluationId)
	{
		return service.SubmitEvaluationAsync(evaluationId);
	}

	[HttpPost("evaluations/{evaluationId:guid}/approve")]
	public Task ApproveEvaluationAsync(Guid evaluationId)
	{
		return service.ApproveEvaluationAsync(evaluationId);
	}
}

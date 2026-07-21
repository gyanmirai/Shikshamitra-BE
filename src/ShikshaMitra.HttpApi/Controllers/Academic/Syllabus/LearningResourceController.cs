using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Syllabus;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.Syllabus;

[Authorize]
[ApiController]
[Route("api/academic/learning-resources")]
public class LearningResourceController(ILearningResourceAppService learningResourceAppService) : AbpController
{
	[HttpPost]
	[ProducesResponseType(typeof(LearningResourceDto), 200)]
	public Task<LearningResourceDto> CreateAsync([FromBody] CreateUpdateLearningResourceDto input)
	{
		return learningResourceAppService.CreateAsync(input);
	}

	[HttpGet]
	[ProducesResponseType(typeof(List<LearningResourceDto>), 200)]
	public Task<List<LearningResourceDto>> GetListAsync([FromQuery] GetLearningResourcesInput input)
	{
		return learningResourceAppService.GetListAsync(input);
	}

	[HttpGet("{id:guid}")]
	[ProducesResponseType(typeof(LearningResourceDto), 200)]
	[ProducesResponseType(404)]
	public Task<LearningResourceDto> GetAsync(Guid id)
	{
		return learningResourceAppService.GetAsync(id);
	}

	[HttpPut("{id:guid}")]
	[ProducesResponseType(typeof(LearningResourceDto), 200)]
	[ProducesResponseType(404)]
	public Task<LearningResourceDto> UpdateAsync(Guid id, [FromBody] CreateUpdateLearningResourceDto input)
	{
		return learningResourceAppService.UpdateAsync(id, input);
	}

	[HttpPost("{id:guid}/submit")]
	public Task SubmitAsync(Guid id)
	{
		return learningResourceAppService.SubmitAsync(id);
	}

	[HttpPost("{id:guid}/publish")]
	public Task PublishAsync(Guid id, [FromBody] LearningResourceReviewDto input)
	{
		return learningResourceAppService.PublishAsync(id, input);
	}

	[HttpPost("{id:guid}/reject")]
	public Task RejectAsync(Guid id, [FromBody] LearningResourceReviewDto input)
	{
		return learningResourceAppService.RejectAsync(id, input);
	}

	[HttpPost("{id:guid}/archive")]
	public Task ArchiveAsync(Guid id, [FromBody] LearningResourceArchiveDto input)
	{
		return learningResourceAppService.ArchiveAsync(id, input);
	}

	[HttpPost("{id:guid}/restore")]
	public Task RestoreAsync(Guid id)
	{
		return learningResourceAppService.RestoreAsync(id);
	}

	[HttpGet("health")]
	[ProducesResponseType(typeof(LearningResourceHealthDto), 200)]
	public Task<LearningResourceHealthDto> GetHealthAsync([FromQuery] GetLearningResourcesInput input)
	{
		return learningResourceAppService.GetHealthAsync(input);
	}
}

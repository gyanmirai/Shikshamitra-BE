//using Microsoft.AspNetCore.Mvc;
//using System;
//using System.Collections.Generic;
//using System.Threading.Tasks;
//using Volo.Abp;
//using Volo.Abp.AspNetCore.Mvc;

//namespace ShikshaMitra.Controllers.Academic.Syllabus;

//[RemoteService]
//[Area("Academic")]
//[Route("api/academic/learning-resources")]
//public class LearningResourceController(ILearningResource) : AbpController
//{
//	[HttpPost]
//	public Task<LearningResourceDto> CreateAsync([FromBody] CreateUpdateLearningResourceDto input)
//	{
//		return learningResourceAppService.CreateAsync(input);
//	}

//	[HttpGet("/standards/{standardId}/subjects/{subjectId}/learning-resources")]
//	public Task<List<LearningResourceDto>> GetByStandardSubjectIdAsync(Guid standardId, Guid subjectId)
//	{
//		return learningResourceAppService.GetByStandardSubjectIdAsync(standardId, subjectId);
//	}

//	[HttpPut("{id}")]
//	public Task<LearningResourceDto> UpdateAsync(Guid id, [FromBody] CreateUpdateLearningResourceDto input)
//	{
//		return learningResourceAppService.UpdateAsync(id, input);
//	}

//	[HttpDelete("{id}")]
//	public Task DeleteAsync(Guid id)
//	{
//		return learningResourceAppService.DeleteAsync(id);
//	}

//	[HttpPost("resource-types")]
//	public Task<ResourceTypeDto> CreateResourceTypeAsync([FromBody] CreateUpdateResourceTypeDto input)
//	{
//		return learningResourceAppService.CreateResourceTypeAsync(input);
//	}

//	[HttpGet("resource-types")]
//	public Task<List<ResourceTypeDto>> GetResourceTypesAsync()
//	{
//		return learningResourceAppService.GetResourceTypesAsync();
//	}

//	[HttpPut("resource-types/{id}")]
//	public Task<ResourceTypeDto> UpdateResourceTypeAsync(Guid id, [FromBody] CreateUpdateResourceTypeDto input)
//	{
//		return learningResourceAppService.UpdateResourceTypeAsync(id, input);
//	}

//	[HttpDelete("resource-types/{id}")]
//	public Task DeleteResourceTypeAsync(Guid id)
//	{
//		return learningResourceAppService.DeleteResourceTypeAsync(id);
//	}
//}

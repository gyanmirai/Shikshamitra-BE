using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Syllabus;
using ShikshaMitra.Common;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp;

namespace ShikshaMitra.Controllers.Academic.Syllabus;

[RemoteService]
[Area("Academic")]
[Route("api/academic/curriculum/chapters")]
public class ChapterController(IChapterAppService chapterAppService) : AbpController
{
	[HttpPost]
	public Task<ApiSuccessResponseDto> CreateAsync(
		[FromQuery] Guid standardId,
		[FromQuery] Guid subjectId,
		[FromBody] CreateChapterDto input)
	{
		return chapterAppService.CreateAsync(standardId, subjectId, input);
	}

	[HttpGet]
	public Task<List<ChapterDto>> GetAsync(
		[FromQuery] Guid standardId,
		[FromQuery] Guid subjectId)
	{
		return chapterAppService.GetByStandardAndSubjectAsync(standardId, subjectId);
	}

	[HttpPut("{id}")]
	public Task<ChapterDto> UpdateAsync(Guid id, [FromBody] CreateUpdateChapterDto input)
	{
		return chapterAppService.UpdateAsync(id, input);
	}

	[HttpDelete("{id}")]
	public Task DeleteAsync(Guid id)
	{
		return chapterAppService.DeleteAsync(id);
	}
}


using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Syllabus;
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
	public Task<ChapterDto> CreateAsync([FromBody] CreateUpdateChapterDto input)
	{
		return chapterAppService.CreateAsync(input);
	}

	[HttpGet("{assignedStandardId}")]
	public Task<List<ChapterDto>> Get(Guid assignedStandardId)
	{
		return chapterAppService.GetByAssignedSubjectIdAsync(assignedStandardId);
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

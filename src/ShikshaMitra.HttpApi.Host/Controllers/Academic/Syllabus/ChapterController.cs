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
[Route("api/academic/chapters")]
public class ChapterController(IChapterAppService chapterAppService) : AbpController
{
	[HttpPost]
	public Task<ChapterDto> CreateAsync([FromBody] CreateUpdateChapterDto input)
	{
		return chapterAppService.CreateAsync(input);
	}

	[HttpGet("/api/academic/standards/{standardId}/subjects/{subjectId}/chapters")]
	public Task<List<ChapterDto>> GetByStandardSubjectIdAsync(Guid standardId, Guid subjectId)
	{
		return chapterAppService.GetByStandardSubjectIdAsync(standardId, subjectId);
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

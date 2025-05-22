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

	[HttpGet("by-standard-subject/{standardSubjectId}")]
	public Task<List<ChapterDto>> GetByStandardSubjectIdAsync(Guid standardSubjectId)
	{
		return chapterAppService.GetByStandardSubjectIdAsync(standardSubjectId);
	}

	[HttpPut("{id}")]
	public Task<ChapterDto> UpdateAsync(Guid id, [FromBody] CreateUpdateChapterDto input)
	{
		return chapterAppService.UpdateAsync(id, input);
	}

}

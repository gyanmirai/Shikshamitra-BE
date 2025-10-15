using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Setting;
using ShikshaMitra.Academic.Syllabus;
using System;
using System.Threading.Tasks;
using Volo.Abp;
namespace ShikshaMitra.Controllers.Academic.Curriculum;

[RemoteService]
[Area("Academic")]
[Route("api/academic/curriculum")]
public class ChapterrController(IChapterrAppService chapterrAppService) : ControllerBase
{

	[HttpPost("chapter")]
	public async Task<IActionResult> CreateChapter(
		[FromBody] CreateChapterDto createChapterDto
	)
	{
		var chapter = await chapterrAppService.CreateChapterAsync(createChapterDto);
		return Ok(chapter);
	}

	[HttpGet("standards/{standardId}/subjects/{subjectId}/chapters")]
	public async Task<IActionResult> GetChapters(
		Guid standardId,
		Guid subjectId)
	{

		if (standardId == Guid.Empty || subjectId == Guid.Empty)
			return BadRequest("StandardId and SubjectId are required.");

		var chapters = await chapterrAppService.GetListChaptersAsync(standardId, subjectId);

		return Ok(chapters);

	}

	[HttpGet("chapter/{id}")]
	public async Task<IActionResult> GetChapterById(
		Guid id
		)
	{
		var chapter = await chapterrAppService.GetByIdChapterAsync(id);
		return Ok(chapter);
	}

	[HttpDelete("chapter/document/{id}")]
	public async Task<IActionResult> DeleteChapterDocument(Guid id)
	{
		if (id == Guid.Empty)
			return BadRequest("Invalid document ID.");

		await chapterrAppService.DeleteByIdChapterDocumentAsync(id);
		return Ok("ChapterDocument has been deleted succesfull");
	}

	[HttpPut("{id}")]
	public async Task<IActionResult> UpdateChapter(
		Guid id, 
		[FromBody] UpdateChapterDto updateChapterDto
		)
	{
		if (updateChapterDto == null)
			return BadRequest("Update data cannot be null.");

		var updatedChapter = await chapterrAppService.UpdateByIdChapterAsync(id, updateChapterDto);
		return Ok(updatedChapter);
	}

	[HttpDelete("chapter/{id}")]
	public async Task<IActionResult> DeleteChapterAsync(Guid id)
	{
		if (id == Guid.Empty)
			return BadRequest("Invalid Chapter Id.");

		await chapterrAppService.DeleteByIdChapterAsync(id);

		return Ok("Chapter has been deleted succesfull");
	}

}

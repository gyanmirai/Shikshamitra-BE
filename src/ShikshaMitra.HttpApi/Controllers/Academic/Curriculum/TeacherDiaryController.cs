using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Curriculum;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.Curriculum;

[Route("api/academic/curriculum/teacher-diaries")]
[Authorize]
public class TeacherDiaryController(ILessonPlanAppService lessonPlanAppService) : AbpController
{
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] GetTeacherDiariesInput input)
    {
        var result = await lessonPlanAppService.GetTeacherDiariesAsync(input);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var diary = await lessonPlanAppService.GetTeacherDiaryAsync(id);
        return Ok(diary);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUpdateTeacherDiaryDto input)
    {
        var diary = await lessonPlanAppService.CreateTeacherDiaryAsync(input);
        return CreatedAtAction(nameof(Get), new { id = diary.Id }, diary);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateUpdateTeacherDiaryDto input)
    {
        var diary = await lessonPlanAppService.UpdateTeacherDiaryAsync(id, input);
        return Ok(diary);
    }

    [HttpPost("{id}/submit")]
    public async Task<IActionResult> Submit(Guid id)
    {
        var diary = await lessonPlanAppService.SubmitTeacherDiaryAsync(id);
        return Ok(diary);
    }
}

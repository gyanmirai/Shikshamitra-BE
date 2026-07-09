using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Curriculum;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.Curriculum;

[Route("api/academic/curriculum/lesson-plans")]
[Authorize]
public class LessonPlanController(ILessonPlanAppService lessonPlanAppService) : AbpController
{
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] GetLessonPlansInput input)
    {
        var result = await lessonPlanAppService.GetListAsync(input);
        return Ok(result);
    }

    [HttpGet("timeline")]
    public async Task<IActionResult> GetTimeline([FromQuery] DateTimeOffset date, [FromQuery] Guid? teacherId = null)
    {
        var timeline = await lessonPlanAppService.GetTimelineAsync(date, teacherId);
        return Ok(timeline);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var plan = await lessonPlanAppService.GetAsync(id);
        return Ok(plan);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLessonPlanDto input)
    {
        var plan = await lessonPlanAppService.CreateAsync(input);
        return CreatedAtAction(nameof(Get), new { id = plan.Id }, plan);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLessonPlanDto input)
    {
        var plan = await lessonPlanAppService.UpdateAsync(id, input);
        return Ok(plan);
    }

    [HttpPost("{id}/submit")]
    public async Task<IActionResult> Submit(Guid id)
    {
        var plan = await lessonPlanAppService.SubmitAsync(id);
        return Ok(plan);
    }

    [HttpPost("bulk-submit")]
    public async Task<IActionResult> BulkSubmit([FromBody] BulkSubmitLessonPlansDto input)
    {
        await lessonPlanAppService.BulkSubmitAsync(input);
        return Ok();
    }

    [HttpPost("{id}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ReviewRemarksDto input)
    {
        var plan = await lessonPlanAppService.ApproveAsync(id, input);
        return Ok(plan);
    }

    [HttpPost("{id}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ReviewRemarksDto input)
    {
        var plan = await lessonPlanAppService.RejectAsync(id, input);
        return Ok(plan);
    }

    [HttpPost("{id}/request-changes")]
    public async Task<IActionResult> RequestChanges(Guid id, [FromBody] ReviewRemarksDto input)
    {
        var plan = await lessonPlanAppService.RequestChangesAsync(id, input);
        return Ok(plan);
    }

    [HttpPost("{id}/homework")]
    public async Task<IActionResult> CreateHomework(Guid id, [FromBody] CreateHomeworkFromLessonPlanDto input)
    {
        var homework = await lessonPlanAppService.CreateHomeworkFromLessonPlanAsync(id, input);
        return Ok(homework);
    }

}

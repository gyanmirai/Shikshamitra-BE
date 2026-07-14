using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.HomeworkAssignment;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.BlobStoring;

namespace ShikshaMitra.Controllers.Academic.HomeworkAssignment;

[ApiController]
[Authorize]
[Route("api/academic/homeworks")]
public class HomeworkController : AbpControllerBase
{
    private readonly IHomeworkAppService _service;
    private readonly IBlobContainer _blobContainer;

    public HomeworkController(IHomeworkAppService service, IBlobContainer blobContainer)
    {
        _service = service;
        _blobContainer = blobContainer;
    }

    /// <summary>
    /// POST api/academic/homeworks?academicMediumId={id}
    /// Creates a new homework in DRAFT status.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(HomeworkCreateResponseDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(422)]
    public async Task<IActionResult> Create(
        [FromQuery] Guid academicMediumId,
        [FromForm] CreateHomeworkDto input,
        IFormFile? attachment
    )
    {
        var blobName = await UploadAttachmentAsync(attachment, "homework");
        
        if (blobName == null && Request.Form.TryGetValue("attachment", out var attachmentStr))
        {
            var strVal = attachmentStr.ToString();
            if (!string.IsNullOrWhiteSpace(strVal))
            {
                blobName = strVal;
            }
        }

        var result = await _service.CreateAsync(academicMediumId, input, blobName);
        return StatusCode(201, result);
    }

    /// <summary>
    /// GET api/academic/homeworks?academicMediumId={id}&standardId={id}&divisionId={id}&subjectId={id}&homeworkDate={date}
    /// Returns paginated list of homework visible on the given date.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(HomeworkListResponseDto), 200)]
    [ProducesResponseType(400)]
    public async Task<HomeworkListResponseDto> GetList(
        [FromQuery] Guid academicMediumId,
        [FromQuery] GetHomeworkListFilterDto filter
    )
    {
        return await _service.GetListAsync(academicMediumId, filter);
    }

    /// <summary>
    /// GET api/academic/homeworks/dashboard?academicMediumId={id}
    /// Returns action dashboard counts for publish, due, overdue, and evaluation queues.
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(LearningTaskDashboardDto), 200)]
    public async Task<LearningTaskDashboardDto> GetDashboard(
        [FromQuery] Guid academicMediumId,
        [FromQuery] LearningTaskDashboardFilterDto input
    )
    {
        return await _service.GetDashboardAsync(academicMediumId, input);
    }

    /// <summary>
    /// GET api/academic/homeworks/{homeworkId}
    /// Returns full homework details.
    /// </summary>
    [HttpGet("{homeworkId:guid}")]
    [ProducesResponseType(typeof(HomeworkResponseDto), 200)]
    [ProducesResponseType(404)]
    public async Task<HomeworkResponseDto> Get(
        Guid homeworkId,
        [FromQuery] Guid? academicMediumId = null
    )
    {
        return await _service.GetAsync(homeworkId, academicMediumId);
    }

    /// <summary>
    /// PUT api/academic/homeworks/{homeworkId}
    /// Full replacement update (DRAFT only).
    /// </summary>
    [HttpPut("{homeworkId:guid}")]
    [ProducesResponseType(typeof(HomeworkResponseDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<HomeworkResponseDto> Update(
        Guid homeworkId,
        [FromForm] UpdateHomeworkDto input,
        IFormFile? attachment
    )
    {
        var blobName = await UploadAttachmentAsync(attachment, "homework");
        
        if (blobName == null && Request.Form.TryGetValue("attachment", out var attachmentStr))
        {
            var strVal = attachmentStr.ToString();
            // If empty string is passed, it means we want to clear the attachment. 
            // So we assign it. In AppService, if attachmentBlobName is empty it will be saved as empty (or we can pass string.Empty).
            // Wait, the AppService does: "attachmentBlobName ?? homework.DocumentUrl".
            // If attachmentBlobName is empty string, it will override DocumentUrl with empty string, which is correct for deletion.
            blobName = strVal;
        }

        return await _service.UpdateAsync(homeworkId, input, blobName);
    }

    /// <summary>
    /// DELETE api/academic/homeworks/{homeworkId}
    /// Soft-deletes homework (DRAFT only).
    /// </summary>
    [HttpDelete("{homeworkId:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Delete(Guid homeworkId)
    {
        await _service.DeleteAsync(homeworkId);
        return NoContent();
    }

    /// <summary>
    /// PATCH api/academic/homeworks/{homeworkId}/status
    /// Transitions status: DRAFT→PUBLISHED or PUBLISHED→CLOSED.
    /// </summary>
    [HttpPatch("{homeworkId:guid}/status")]
    [ProducesResponseType(typeof(HomeworkStatusUpdateResponseDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<HomeworkStatusUpdateResponseDto> ChangeStatus(
        Guid homeworkId,
        [FromBody] HomeworkStatusUpdateDto input
    )
    {
        return await _service.ChangeStatusAsync(homeworkId, input);
    }

    /// <summary>
    /// POST api/academic/homeworks/{homeworkId}/reminder
    /// Queues a reminder for published target divisions and returns the impacted counts.
    /// </summary>
    [HttpPost("{homeworkId:guid}/reminder")]
    [ProducesResponseType(typeof(LearningTaskReminderResponseDto), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<LearningTaskReminderResponseDto> SendReminder(
        Guid homeworkId,
        [FromBody] LearningTaskReminderRequestDto input
    )
    {
        return await _service.SendReminderAsync(homeworkId, input);
    }

    /// <summary>
    /// GET api/academic/homeworks/{homeworkId}/history
    /// Returns human-readable lifecycle history for audit/review panels.
    /// </summary>
    [HttpGet("{homeworkId:guid}/history")]
    [ProducesResponseType(typeof(LearningTaskHistoryDto), 200)]
    [ProducesResponseType(404)]
    public async Task<LearningTaskHistoryDto> GetHistory(Guid homeworkId)
    {
        return await _service.GetHistoryAsync(homeworkId);
    }

    private async Task<string?> UploadAttachmentAsync(IFormFile? file, string prefix)
    {
        if (file == null) return null;

        const long maxSize = 5 * 1024 * 1024;
        if (file.Length > maxSize)
            throw new BusinessException($"ShikshaMitra.{prefix}:AttachmentTooLarge");

        var blobName = $"{prefix}/{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        await using var stream = file.OpenReadStream();
        await _blobContainer.SaveAsync(blobName, stream, overrideExisting: true);
        return blobName;
    }
}

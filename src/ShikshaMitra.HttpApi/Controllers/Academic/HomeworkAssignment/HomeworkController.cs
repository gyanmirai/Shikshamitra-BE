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

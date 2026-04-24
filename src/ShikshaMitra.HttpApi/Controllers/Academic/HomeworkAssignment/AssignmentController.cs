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
[Route("api/academic/assignments")]
public class AssignmentController : AbpControllerBase
{
    private readonly IAssignmentV2AppService _service;
    private readonly IBlobContainer _blobContainer;

    public AssignmentController(IAssignmentV2AppService service, IBlobContainer blobContainer)
    {
        _service = service;
        _blobContainer = blobContainer;
    }

    /// <summary>
    /// POST api/academic/assignments?academicMediumId={id}
    /// Creates a new assignment in DRAFT status.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AssignmentV2CreateResponseDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(422)]
    public async Task<IActionResult> Create(
        [FromQuery] Guid academicMediumId,
        [FromForm] CreateAssignmentV2Dto input,
        IFormFile? attachment
    )
    {
        var blobName = await UploadAttachmentAsync(attachment);
        var result = await _service.CreateAsync(academicMediumId, input, blobName);
        return StatusCode(201, result);
    }

    /// <summary>
    /// GET api/academic/assignments?academicMediumId={id}&standardId={id}&divisionId={id}&subjectId={id}&assignmentDate={date}
    /// Returns paginated list of assignments visible on the given date.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(AssignmentV2ListResponseDto), 200)]
    [ProducesResponseType(400)]
    public async Task<AssignmentV2ListResponseDto> GetList(
        [FromQuery] Guid academicMediumId,
        [FromQuery] GetAssignmentV2ListFilterDto filter
    )
    {
        return await _service.GetListAsync(academicMediumId, filter);
    }

    /// <summary>
    /// GET api/academic/assignments/{assignmentId}
    /// Returns full assignment details.
    /// </summary>
    [HttpGet("{assignmentId:guid}")]
    [ProducesResponseType(typeof(AssignmentV2ResponseDto), 200)]
    [ProducesResponseType(404)]
    public async Task<AssignmentV2ResponseDto> Get(
        Guid assignmentId,
        [FromQuery] Guid? academicMediumId = null
    )
    {
        return await _service.GetAsync(assignmentId, academicMediumId);
    }

    /// <summary>
    /// PUT api/academic/assignments/{assignmentId}
    /// Full replacement update (DRAFT only).
    /// </summary>
    [HttpPut("{assignmentId:guid}")]
    [ProducesResponseType(typeof(AssignmentV2ResponseDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<AssignmentV2ResponseDto> Update(
        Guid assignmentId,
        [FromForm] UpdateAssignmentV2Dto input,
        IFormFile? attachment
    )
    {
        var blobName = await UploadAttachmentAsync(attachment);
        return await _service.UpdateAsync(assignmentId, input, blobName);
    }

    /// <summary>
    /// DELETE api/academic/assignments/{assignmentId}
    /// Soft-deletes assignment (DRAFT only).
    /// </summary>
    [HttpDelete("{assignmentId:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Delete(Guid assignmentId)
    {
        await _service.DeleteAsync(assignmentId);
        return NoContent();
    }

    /// <summary>
    /// PATCH api/academic/assignments/{assignmentId}/status
    /// Transitions status: DRAFT→PUBLISHED or PUBLISHED→CLOSED.
    /// </summary>
    [HttpPatch("{assignmentId:guid}/status")]
    [ProducesResponseType(typeof(AssignmentStatusUpdateResponseDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<AssignmentStatusUpdateResponseDto> ChangeStatus(
        Guid assignmentId,
        [FromBody] AssignmentStatusUpdateDto input
    )
    {
        return await _service.ChangeStatusAsync(assignmentId, input);
    }

    private async Task<string?> UploadAttachmentAsync(IFormFile? file)
    {
        if (file == null) return null;

        const long maxSize = 5 * 1024 * 1024;
        if (file.Length > maxSize)
            throw new BusinessException("ShikshaMitra.Assignment:AttachmentTooLarge");

        var blobName = $"assignment/{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        await using var stream = file.OpenReadStream();
        await _blobContainer.SaveAsync(blobName, stream, overrideExisting: true);
        return blobName;
    }
}

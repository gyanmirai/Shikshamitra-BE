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
[Route("api/academic/homework-submissions")]
public class HomeworkSubmissionController(
    IStudentSubmissionAppService service,
    IBlobContainer blobContainer
) : AbpControllerBase
{
    /// <summary>
    /// POST api/academic/homework-submissions
    /// Student uploads a file submission for a homework. Only valid when homework submissionType is FILE_UPLOAD.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SubmissionUploadResponseDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> UploadHomeworkSubmission(
        [FromForm] UploadHomeworkSubmissionDto input,
        IFormFile file
    )
    {
        var blobName = await UploadFileAsync(file, "homework-submission");
        var result = await service.UploadHomeworkSubmissionAsync(input, blobName);
        return StatusCode(201, result);
    }

    private async Task<string> UploadFileAsync(IFormFile file, string prefix)
    {
        const long maxSize = 10 * 1024 * 1024;
        if (file.Length > maxSize)
            throw new BusinessException($"ShikshaMitra.{prefix}:FileTooLarge");

        var blobName = $"{prefix}/{System.Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        await using var stream = file.OpenReadStream();
        await blobContainer.SaveAsync(blobName, stream, overrideExisting: true);
        return blobName;
    }
}

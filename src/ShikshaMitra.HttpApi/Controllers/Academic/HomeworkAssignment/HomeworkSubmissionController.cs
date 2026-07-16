using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.HomeworkAssignment;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.HomeworkAssignment;

[ApiController]
[Authorize]
[Route("api/academic/homework-submissions")]
public class HomeworkSubmissionController(
    IStudentSubmissionAppService service
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
        IFormFile? file
    )
    {
        EnsureNoDirectFileUpload(file);
        var result = await service.UploadHomeworkSubmissionAsync(
            input,
            RequireUploadedFileName(input.FileName),
            input.OriginalFileName
        );
        return StatusCode(201, result);
    }

    private static void EnsureNoDirectFileUpload(IFormFile? file)
    {
        if (file != null)
            throw new UserFriendlyException("Upload files with /api/files first, then submit the returned file name.");
    }

    private static string RequireUploadedFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new UserFriendlyException("Uploaded file name is required.");

        return fileName;
    }
}

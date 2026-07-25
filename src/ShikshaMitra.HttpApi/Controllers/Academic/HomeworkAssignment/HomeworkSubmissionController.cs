using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Common.Files;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.HomeworkAssignment;

[ApiController]
[Authorize]
[Route("api/academic/homework-submissions")]
public class HomeworkSubmissionController(
    IStudentSubmissionAppService service,
    IFileStorageAppService fileStorageAppService
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
    [Consumes("multipart/form-data")]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = 104857600)]
    public async Task<IActionResult> UploadHomeworkSubmission(
        [FromForm] UploadHomeworkSubmissionDto input,
        IFormFile? file
    )
    {
        var uploadedFile = await ResolveUploadedFileAsync(input, file);
        var result = await service.UploadHomeworkSubmissionAsync(
            input,
            uploadedFile.FileName,
            uploadedFile.OriginalFileName
        );
        return StatusCode(201, result);
    }

    private async Task<(string FileName, string? OriginalFileName)> ResolveUploadedFileAsync(
        UploadHomeworkSubmissionDto input,
        IFormFile? file)
    {
        if (file == null)
        {
            return (RequireUploadedFileName(input.FileName), input.OriginalFileName);
        }

        if (file.Length == 0)
        {
            throw new UserFriendlyException("Uploaded file cannot be empty.");
        }

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);

        var fileName = await fileStorageAppService.UploadAsync(file.FileName, ms.ToArray());
        var originalFileName = string.IsNullOrWhiteSpace(input.OriginalFileName)
            ? file.FileName
            : input.OriginalFileName;

        return (fileName, originalFileName);
    }

    private static string RequireUploadedFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new UserFriendlyException("Uploaded file name is required.");

        return fileName;
    }
}

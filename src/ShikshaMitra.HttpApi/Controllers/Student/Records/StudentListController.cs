using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Validation;

namespace ShikshaMitra.HttpApi.Controllers.Students.Records;

[ApiController]
[Authorize]
[Route("api/students/records/list")]
public class StudentListController(
	IStudentListAppService studentListAppService
) : AbpController
{
	private const long MaxPhotoUploadBytes = 5 * 1024 * 1024;

	[HttpGet("{divisionId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View)]
	public async Task<PagedResultDto<StudentDto>> GetStudentsByDivision(
		Guid divisionId,
		[FromQuery] int skipCount = 0,
		[FromQuery] int maxResultCount = 10,
		[FromQuery] string? searchTerm = null)
	{
		return await studentListAppService.GetStudentsByDivisionAsync(divisionId, skipCount, maxResultCount, searchTerm);
	}

	[HttpGet("/api/students/records/dashboard-summary")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View)]
	public async Task<StudentDashboardSummaryDto> GetDashboardSummary()
	{
		return await studentListAppService.GetDashboardSummaryAsync();
	}

	[HttpGet("/api/students/records/attendance-dashboard-summary")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View)]
	public async Task<StudentAttendanceDashboardSummaryDto> GetAttendanceDashboardSummary(
		[FromQuery] GetStudentAttendanceDashboardSummaryInput input)
	{
		return await studentListAppService.GetAttendanceDashboardSummaryAsync(input);
	}

	[HttpPost("/api/students/records/{studentId}/photo")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.Add)]
	[Consumes("multipart/form-data")]
	public async Task<StudentPhotoUploadResultDto> UploadPhoto(
		Guid studentId,
		[FromForm] StudentPhotoUploadRequest request)
	{
		if (request.File == null || request.File.Length == 0)
		{
			throw new BadHttpRequestException("No photo was uploaded.");
		}

		if (request.File.Length > MaxPhotoUploadBytes)
		{
			throw new BadHttpRequestException("Uploaded photo exceeds the allowed size.");
		}

		var content = await ReadBoundedAsync(request.File, MaxPhotoUploadBytes, HttpContext.RequestAborted);

		return await studentListAppService.UploadPhotoAsync(studentId, new UploadStudentPhotoDto
		{
			FileName = request.File.FileName,
			ContentType = request.File.ContentType,
			Content = content
		});
	}

	[HttpGet("/api/students/records/{studentId}/photo")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View)]
	public async Task<IActionResult> GetPhoto(Guid studentId)
	{
		var photo = await studentListAppService.GetPhotoAsync(studentId);
		if (photo == null)
		{
			return NotFound();
		}

		return File(photo.Content, photo.ContentType, photo.FileName);
	}

	[HttpPut("{studentId}/status")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.Add)]
	public async Task ChangeStudentStatus(Guid studentId, [FromBody] ChangeStudentStatusDto input)
	{
		await studentListAppService.ChangeStudentStatusAsync(studentId, input);
	}

	[HttpGet("{studentId}/status/history")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View)]
	public async Task<List<StudentStatusHistoryDto>> GetStatusHistory(Guid studentId)
	{
		return await studentListAppService.GetStatusHistoryAsync(studentId);
	}

	[HttpGet("by-scope")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View)]
	public async Task<List<StudentLookupDto>> GetStudentsByScope([FromQuery] GetStudentsByScopeRequest input)
	{
		return await studentListAppService.GetStudentsByScopeAsync(input);
	}

	private static async Task<byte[]> ReadBoundedAsync(
		IFormFile file,
		long maxBytes,
		CancellationToken cancellationToken)
	{
		await using var inputStream = file.OpenReadStream();
		using var memoryStream = new MemoryStream(capacity: (int)Math.Min(file.Length, 1024 * 1024));
		var buffer = new byte[81920];
		long totalRead = 0;

		while (true)
		{
			var read = await inputStream.ReadAsync(buffer, cancellationToken);
			if (read == 0)
			{
				break;
			}

			totalRead += read;
			if (totalRead > maxBytes)
			{
				throw new BadHttpRequestException("Uploaded photo exceeds the allowed size.");
			}

			await memoryStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
		}

		return memoryStream.ToArray();
	}
}

public class StudentPhotoUploadRequest
{
	[Required]
	[DisableValidation]
	public IFormFile? File { get; set; }
}

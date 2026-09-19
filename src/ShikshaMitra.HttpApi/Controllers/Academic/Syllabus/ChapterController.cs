using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Syllabus;
using ShikshaMitra.Common;
using ShikshaMitra.Common.Files;
using ShikshaMitra.Models;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Validation;

namespace ShikshaMitra.Controllers.Academic.Syllabus;

[Authorize]
[ApiController]
[Route("api/academic/curriculum/chapters")]
public class ChapterController(
	IChapterAppService chapterAppService,
	IFileStorageAppService fileStorageAppService,
	ILogger<ChapterController> logger) : AbpController
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	[HttpPost]
	public Task<ApiSuccessResponseDto> CreateAsync(
		[FromQuery] Guid standardId,
		[FromQuery] Guid subjectId,
		[FromBody] List<CreateChapterDto> input)
	{
		return chapterAppService.CreateAsync(standardId, subjectId, input);
	}

	[HttpPost("with-attachments")]
	[Consumes("multipart/form-data")]
	[DisableRequestSizeLimit]
	[RequestFormLimits(MultipartBodyLengthLimit = 104857600)]
	public async Task<ApiSuccessResponseDto> CreateWithAttachmentsAsync(
		[FromQuery] Guid standardId,
		[FromQuery] Guid subjectId,
		[FromForm] ChapterMultipartRequest request)
	{
		var input = DeserializePayload<CreateChapterDto>(request.Payload);
		var uploadedFiles = await UploadFilesAsync(request.Files);

		try
		{
			input.Documents = MergeDocuments(input.Documents, uploadedFiles);
			return await chapterAppService.CreateAsync(standardId, subjectId, [input]);
		}
		catch
		{
			await DeleteUploadedFilesAsync(uploadedFiles);
			throw;
		}
	}

	[HttpGet]
	public Task<List<ChapterDto>> GetAsync(
		[FromQuery] Guid standardId,
		[FromQuery] Guid subjectId,
		[FromQuery] Guid? divisionId = null)
	{
		return chapterAppService.GetByStandardAndSubjectAsync(standardId, subjectId, divisionId);
	}

	[HttpPut("{id}")]
	public Task<ChapterDto> UpdateAsync(Guid id, [FromBody] CreateUpdateChapterDto input)
	{
		return chapterAppService.UpdateAsync(id, input);
	}

	[HttpPut("{id}/with-attachments")]
	[Consumes("multipart/form-data")]
	[DisableRequestSizeLimit]
	[RequestFormLimits(MultipartBodyLengthLimit = 104857600)]
	public async Task<ChapterDto> UpdateWithAttachmentsAsync(
		Guid id,
		[FromForm] ChapterMultipartRequest request)
	{
		var input = DeserializePayload<CreateUpdateChapterDto>(request.Payload);
		var uploadedFiles = await UploadFilesAsync(request.Files);

		try
		{
			input.Documents = MergeDocuments(input.Documents, uploadedFiles);
			return await chapterAppService.UpdateAsync(id, input);
		}
		catch
		{
			await DeleteUploadedFilesAsync(uploadedFiles);
			throw;
		}
	}

	[HttpDelete("{id}")]
	public Task DeleteAsync(Guid id)
	{
		return chapterAppService.DeleteAsync(id);
	}

	private static T DeserializePayload<T>(string payload)
	{
		try
		{
			return JsonSerializer.Deserialize<T>(payload, JsonOptions)
				?? throw CreatePayloadValidationException();
		}
		catch (JsonException)
		{
			throw CreatePayloadValidationException();
		}
	}

	private static AbpValidationException CreatePayloadValidationException()
	{
		const string message = "The 'Payload' field does not contain valid chapter JSON.";
		return new AbpValidationException(
			message,
			[new ValidationResult(message, [nameof(ChapterMultipartRequest.Payload)])]);
	}

	private async Task<List<string>> UploadFilesAsync(IEnumerable<IFormFile> files)
	{
		var uploadedFiles = new List<string>();
		try
		{
			foreach (var file in files.Where(file => file.Length > 0))
			{
				await using var stream = new MemoryStream();
				await file.CopyToAsync(stream);
				uploadedFiles.Add(await fileStorageAppService.UploadAsync(file.FileName, stream.ToArray()));
			}

			return uploadedFiles;
		}
		catch
		{
			await DeleteUploadedFilesAsync(uploadedFiles);
			throw;
		}
	}

	private static List<string> MergeDocuments(
		IEnumerable<string>? existingDocuments,
		IEnumerable<string> uploadedFiles)
	{
		return (existingDocuments ?? [])
			.Concat(uploadedFiles)
			.Where(document => !string.IsNullOrWhiteSpace(document))
			.Distinct(StringComparer.Ordinal)
			.ToList();
	}

	private async Task DeleteUploadedFilesAsync(IEnumerable<string> uploadedFiles)
	{
		foreach (var uploadedFile in uploadedFiles)
		{
			try
			{
				await fileStorageAppService.DeleteAsync(uploadedFile);
			}
			catch (Exception exception)
			{
				logger.LogWarning(
					exception,
					"Could not remove chapter attachment {UploadedFile} after the chapter request failed.",
					uploadedFile);
			}
		}
	}
}


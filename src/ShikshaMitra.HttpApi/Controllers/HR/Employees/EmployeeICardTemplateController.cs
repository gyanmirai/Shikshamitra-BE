using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Employees.ICards;
using ShikshaMitra.HR;
using ShikshaMitra.HttpApi.Models;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Timing;

namespace ShikshaMitra.Controllers.HR.Employees;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/employees/i-card/templates")]
[Authorize]
public class EmployeeICardTemplateController : AbpControllerBase
{
	private const long MaxAssetSize = 2 * 1024 * 1024;
	private readonly IEmployeeICardTemplateAppService _appService;
	private readonly IClock _clock;

	public EmployeeICardTemplateController(IEmployeeICardTemplateAppService appService, IClock clock)
	{
		_appService = appService;
		_clock = clock;
	}

	[HttpGet]
	[ProducesResponseType(typeof(EmployeeICardTemplateListResponse), StatusCodes.Status200OK)]
	public Task<EmployeeICardTemplateListResponse> GetListAsync(
		[FromQuery] Guid? academicYearId = null,
		[FromQuery] Guid? mediumStageId = null,
		[FromQuery, Range(1, int.MaxValue)] int page = 1,
		[FromQuery, Range(1, 100)] int pageSize = 20,
		[FromQuery] string? search = null)
	{
		return _appService.GetListAsync(academicYearId, mediumStageId, page, pageSize, search);
	}

	[HttpGet("{id}")]
	[ProducesResponseType(typeof(EmployeeICardTemplateDto), StatusCodes.Status200OK)]
	public Task<EmployeeICardTemplateDto> GetAsync(Guid id)
	{
		return _appService.GetAsync(id);
	}

	[HttpPost]
	[ProducesResponseType(typeof(EmployeeICardTemplateDto), StatusCodes.Status201Created)]
	public Task<EmployeeICardTemplateDto> CreateAsync([FromBody] EmployeeICardTemplateRequest input)
	{
		return _appService.CreateAsync(input);
	}

	[HttpPut("{id}")]
	[ProducesResponseType(typeof(EmployeeICardTemplateDto), StatusCodes.Status200OK)]
	public Task<EmployeeICardTemplateDto> UpdateAsync(Guid id, [FromBody] EmployeeICardTemplateRequest input)
	{
		return _appService.UpdateAsync(id, input);
	}

	[HttpPost("assets")]
	[Consumes("multipart/form-data")]
	[ProducesResponseType(typeof(EmployeeICardAssetUploadResult), StatusCodes.Status200OK)]
	public async Task<EmployeeICardAssetUploadResult> UploadAssetAsync([FromForm] EmployeeICardAssetUploadRequest request)
	{
		var file = request.File;
		if (file is null || file.Length == 0 || file.Length > MaxAssetSize)
		{
			throw new BusinessException(HRErrorCodes.EmployeeICardAssetInvalid);
		}

		await using var stream = file.OpenReadStream();
		await using var memoryStream = new MemoryStream();
		await stream.CopyToAsync(memoryStream);
		return await _appService.UploadAssetAsync(file.FileName, file.ContentType, memoryStream.ToArray());
	}

	[HttpGet("assets")]
	public async Task<IActionResult> DownloadAssetAsync([FromQuery, Required] string blobName)
	{
		var asset = await _appService.DownloadAssetAsync(blobName);
		return File(asset.Content, asset.ContentType, asset.FileName);
	}

	[HttpGet("school-logo")]
	public async Task<IActionResult> SchoolLogoAsync([FromQuery] Guid? mediumStageId)
	{
		var asset = await _appService.GetSchoolLogoAsync(mediumStageId);
		return asset == null ? NoContent() : File(asset.Content, asset.ContentType, asset.FileName);
	}

	[HttpPost("{id}/pdf")]
	public async Task<IActionResult> GeneratePdfAsync(Guid id, [FromBody] EmployeeICardPdfRequest input)
	{
		var bytes = await _appService.GenerateEmployeeICardsPdfAsync(id, input);
		return File(bytes, "application/pdf", $"employee-i-cards-{_clock.Now:yyyyMMddHHmmss}.pdf");
	}

	[HttpDelete("{id}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public Task DeleteAsync(Guid id)
	{
		return _appService.DeleteAsync(id);
	}

}

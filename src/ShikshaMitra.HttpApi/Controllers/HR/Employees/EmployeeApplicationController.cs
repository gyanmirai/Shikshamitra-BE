using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees.Onboarding;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.Models;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Validation;

namespace ShikshaMitra.Controllers.HR.Employees;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/employee-applications")]
[Authorize]
public class EmployeeApplicationController(IEmployeeApplicationAppService appService) : AbpControllerBase
{
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeOnboarding.View)]
	[ProducesResponseType(typeof(PagedResultDto<EmployeeApplicationListItemDto>), StatusCodes.Status200OK)]
	public async Task<PagedResultDto<EmployeeApplicationListItemDto>> GetListAsync(
		[FromQuery] GetEmployeeApplicationListInput input)
	{
		return await appService.GetListAsync(input);
	}

	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeOnboarding.Add)]
	[ProducesResponseType(typeof(EmployeeApplicationInviteDto), StatusCodes.Status201Created)]
	public async Task<EmployeeApplicationInviteDto> CreateAsync(
		[FromBody, Required] CreateEmployeeApplicationInviteDto input)
	{
		var result = await appService.CreateAsync(input);
		Response.StatusCode = StatusCodes.Status201Created;
		return result;
	}

	[HttpGet("{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeOnboarding.View)]
	[ProducesResponseType(typeof(EmployeeApplicationDto), StatusCodes.Status200OK)]
	public async Task<EmployeeApplicationDto> GetAsync([FromRoute, Required] Guid id)
	{
		return await appService.GetAsync(id);
	}

	[HttpPut("{id:guid}/status")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeOnboarding.Edit)]
	[ProducesResponseType(typeof(EmployeeApplicationDto), StatusCodes.Status200OK)]
	public async Task<EmployeeApplicationDto> UpdateStatusAsync(
		[FromRoute, Required] Guid id,
		[FromBody, Required] EmployeeApplicationStatusUpdateDto input)
	{
		return await appService.UpdateStatusAsync(id, input);
	}

	[HttpPost("{id:guid}/regenerate-link")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeOnboarding.Edit)]
	[ProducesResponseType(typeof(EmployeeApplicationInviteDto), StatusCodes.Status200OK)]
	public async Task<EmployeeApplicationInviteDto> RegenerateLinkAsync([FromRoute, Required] Guid id)
	{
		return await appService.RegenerateLinkAsync(id);
	}

	[HttpPost("{id:guid}/finalize")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeOnboarding.Finalize)]
	[ProducesResponseType(typeof(EmployeeApplicationFinalizeResultDto), StatusCodes.Status200OK)]
	public async Task<EmployeeApplicationFinalizeResultDto> FinalizeAsync(
		[FromRoute, Required] Guid id,
		[FromBody, Required] EmployeeApplicationFinalizeRequest request)
	{
		ValidateRequest(request.PersonalInfo, "Personal info validation failed.");

		return await appService.FinalizeAsync(id, new FinalizeEmployeeApplicationDto
		{
			PersonalInfo = PersonalInfoContractMapper.ToCreateDto(request.PersonalInfo),
			WorkExperiences = request.WorkExperiences,
			UseSubmittedPhoto = request.UseSubmittedPhoto,
			AttachResumeAsDocument = request.AttachResumeAsDocument
		});
	}

	[HttpGet("{id:guid}/resume")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeOnboarding.View)]
	[ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetResumeAsync([FromRoute, Required] Guid id)
	{
		var file = await appService.GetResumeAsync(id);
		return file == null
			? NotFound()
			: File(file.Content, file.ContentType, file.FileName);
	}

	[HttpGet("{id:guid}/photo")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeOnboarding.View)]
	[ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetPhotoAsync([FromRoute, Required] Guid id)
	{
		var file = await appService.GetPhotoAsync(id);
		return file == null
			? NotFound()
			: File(file.Content, file.ContentType, file.FileName);
	}

	[HttpDelete("{id:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeOnboarding.Delete)]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	public async Task<IActionResult> DeleteAsync([FromRoute, Required] Guid id)
	{
		await appService.DeleteAsync(id);
		return NoContent();
	}

	private static void ValidateRequest(object request, string message)
	{
		var validationResults = new List<ValidationResult>();
		if (!Validator.TryValidateObject(request, new ValidationContext(request), validationResults, true))
		{
			throw new AbpValidationException(message, validationResults);
		}
	}
}

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/public/employee-applications")]
[AllowAnonymous]
public class PublicEmployeeApplicationController(IEmployeeApplicationAppService appService) : AbpControllerBase
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	[HttpGet("{token}")]
	[ProducesResponseType(typeof(PublicEmployeeApplicationDto), StatusCodes.Status200OK)]
	public async Task<PublicEmployeeApplicationDto> GetAsync([FromRoute, Required] string token)
	{
		return await appService.GetPublicAsync(token);
	}

	[HttpPost("{token}/submit")]
	[Consumes("multipart/form-data")]
	[ProducesResponseType(typeof(EmployeeApplicationDto), StatusCodes.Status200OK)]
	public async Task<EmployeeApplicationDto> SubmitAsync(
		[FromRoute, Required] string token,
		[FromForm] PublicEmployeeApplicationSubmitRequest request)
	{
		var input = DeserializeSubmitData(request.Data);
		ValidateSubmitRequest(input);

		return await appService.SubmitPublicAsync(
			token,
			input,
			await ToFileInputAsync(request.Resume),
			await ToFileInputAsync(request.Photo));
	}

	private static PublicEmployeeApplicationSubmitDto DeserializeSubmitData(string data)
	{
		try
		{
			var input = JsonSerializer.Deserialize<PublicEmployeeApplicationSubmitDto>(data, JsonOptions);
			return input ?? throw new JsonException();
		}
		catch (JsonException)
		{
			throw new AbpValidationException(
				"The 'data' field does not contain valid employee application JSON.",
				[
					new ValidationResult(
						"The 'data' field does not contain valid employee application JSON.",
						[nameof(PublicEmployeeApplicationSubmitRequest.Data)])
				]);
		}
	}

	private static void ValidateSubmitRequest(PublicEmployeeApplicationSubmitDto input)
	{
		var validationResults = new List<ValidationResult>();
		if (!Validator.TryValidateObject(input, new ValidationContext(input), validationResults, true))
		{
			throw new AbpValidationException("Employee application validation failed.", validationResults);
		}
	}

	private static async Task<EmployeeApplicationFileInput?> ToFileInputAsync(IFormFile? file)
	{
		if (file == null || file.Length == 0)
		{
			return null;
		}

		await using var memoryStream = new MemoryStream();
		await file.CopyToAsync(memoryStream);
		return new EmployeeApplicationFileInput
		{
			FileName = file.FileName,
			ContentType = file.ContentType,
			Content = memoryStream.ToArray(),
			Size = file.Length
		};
	}
}

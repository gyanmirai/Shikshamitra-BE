using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Documents;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.Models;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Employees;

/// <summary>
/// Employee personal information management controller
/// </summary>
[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/employees")]
[Authorize]
public class PersonalInfoController : AbpControllerBase
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	private readonly IEmployeePersonalInfoAppService _personalInfoAppService;
	private readonly IEmployeeDocumentAppService _documentAppService;
	private readonly IEmployeeAuthorizationService _authorizationService;

	public PersonalInfoController(
		IEmployeePersonalInfoAppService personalInfoAppService,
		IEmployeeDocumentAppService documentAppService,
		IEmployeeAuthorizationService authorizationService)
	{
		_personalInfoAppService = personalInfoAppService;
		_documentAppService = documentAppService;
		_authorizationService = authorizationService;
	}

	/// <summary>
	/// Create employee personal information with an optional profile picture.
	/// Send as multipart/form-data. The <c>data</c> field must contain a JSON string
	/// matching the PersonalInfoRequest schema. The <c>profilePicture</c> field is optional.
	/// </summary>
	[HttpPost("personal-info")]
	[Consumes("multipart/form-data")]
	[ProducesResponseType(typeof(PersonalInfoResponse), StatusCodes.Status201Created)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status403Forbidden)]
	public virtual async Task<PersonalInfoResponse> CreateAsync(
		[FromForm] CreatePersonalInfoWithPictureRequest request)
	{
		PersonalInfoRequest? personalInfo;
		try
		{
			personalInfo = JsonSerializer.Deserialize<PersonalInfoRequest>(request.Data, JsonOptions);
		}
		catch (JsonException)
		{
			throw new UserFriendlyException("The 'data' field does not contain valid PersonalInfoRequest JSON.");
		}

		if (personalInfo is null)
			throw new UserFriendlyException("The 'data' field does not contain valid PersonalInfoRequest JSON.");

		var validationResults = new List<ValidationResult>();
		if (!Validator.TryValidateObject(personalInfo, new ValidationContext(personalInfo), validationResults, true))
		{
			var errors = string.Join("; ", validationResults.ConvertAll(r => r.ErrorMessage));
			throw new UserFriendlyException(errors);
		}

		var createDto = PersonalInfoContractMapper.ToCreateDto(personalInfo);

		EmployeePersonalInfoDto result;
		if (request.ProfilePicture is { Length: > 0 })
		{
			await using var ms = new MemoryStream();
			await request.ProfilePicture.CopyToAsync(ms);

			result = await _personalInfoAppService.CreateWithProfilePictureAsync(
				createDto,
				request.ProfilePicture.FileName,
				ms.ToArray());
		}
		else
		{
			result = await _personalInfoAppService.CreateAsync(createDto);
		}

		return PersonalInfoContractMapper.ToResponse(result);
	}

	/// <summary>
	/// Get employee personal information
	/// </summary>
	[HttpGet("{employeeId}/personal-info")]
	[ProducesResponseType(typeof(PersonalInfoResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
	public virtual async Task<PersonalInfoResponse> GetAsync([FromRoute, Required] Guid employeeId)
	{
		await _authorizationService.CheckEmployeeAccessAsync(employeeId);
		var result = await _personalInfoAppService.GetAsync(employeeId);
		return PersonalInfoContractMapper.ToResponse(result);
	}

	/// <summary>
	/// Update employee personal information
	/// </summary>
	[HttpPut("{employeeId}/personal-info")]
	[ProducesResponseType(typeof(PersonalInfoResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status400BadRequest)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status403Forbidden)]
	[ProducesResponseType(typeof(Result), StatusCodes.Status404NotFound)]
	public virtual async Task<PersonalInfoResponse> UpdateAsync(
		[FromRoute, Required] Guid employeeId,
		[FromBody, Required] PersonalInfoRequest request)
	{
		await _authorizationService.CheckEmployeeModifyAccessAsync(employeeId);
		var updateDto = PersonalInfoContractMapper.ToUpdateDto(request);
		var result = await _personalInfoAppService.UpdateAsync(employeeId, updateDto);
		return PersonalInfoContractMapper.ToResponse(result);
	}

	/// <summary>
	/// Download employee profile picture
	/// </summary>
	[HttpGet("{employeeId}/profile-picture")]
	[ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public virtual async Task<IActionResult> GetProfilePictureAsync(
		[FromRoute, Required] Guid employeeId)
	{
		await _authorizationService.CheckEmployeeAccessAsync(employeeId);

		var document = await _documentAppService.GetProfilePictureAsync(employeeId);
		if (document is null)
			return NotFound();

		return File(document.Content, document.ContentType, document.FileName);
	}
}

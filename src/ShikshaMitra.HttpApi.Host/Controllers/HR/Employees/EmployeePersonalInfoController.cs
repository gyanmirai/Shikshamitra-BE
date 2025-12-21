using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.Models;
using System;
using System.IO;
using System.Threading.Tasks;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Employees;

/// <summary>
/// Controller for managing employee personal information.
/// </summary>
[Route("employee-personal-info")]
[Authorize]
public class EmployeePersonalInfoController(
	IEmployeePersonalInfoAppService employeePersonalInfoAppService,
	IEmployeeAppService employeeAppService)
	: AbpController
{
	/// <summary>
	/// Create a new employee with personal information.
	/// </summary>
	/// <param name="input">Employee personal information</param>
	/// <returns>Created employee personal information</returns>
	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add)]
	[ProducesResponseType(typeof(EmployeePersonalInfoDto), StatusCodes.Status201Created)]
	public async Task<IActionResult> CreateAsync([FromBody] CreateEmployeePersonalInfoDto input)
	{
		var result = await employeePersonalInfoAppService.CreateAsync(input);
		return CreatedAtAction(nameof(GetAsync), new { id = result.Id }, result);
	}

	/// <summary>
	/// Get employee personal information by ID.
	/// </summary>
	/// <param name="id">Employee ID</param>
	/// <returns>Employee personal information</returns>
	[HttpGet("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View)]
	[ProducesResponseType(typeof(EmployeePersonalInfoDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<EmployeePersonalInfoDto> GetAsync(Guid id)
	{
		return await employeePersonalInfoAppService.GetAsync(id);
	}

	/// <summary>
	/// Update employee personal information.
	/// </summary>
	/// <param name="id">Employee ID</param>
	/// <param name="input">Updated employee personal information</param>
	/// <returns>Updated employee personal information</returns>
	[HttpPut("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add)]
	[ProducesResponseType(typeof(EmployeePersonalInfoDto), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<EmployeePersonalInfoDto> UpdateAsync(Guid id, [FromBody] UpdateEmployeePersonalInfoDto input)
	{
		return await employeePersonalInfoAppService.UpdateAsync(id, input);
	}

	/// <summary>
	/// Get employee profile picture.
	/// </summary>
	/// <param name="id">Employee ID</param>
	/// <returns>Profile picture file</returns>
	[HttpGet("{id}/profile-picture")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View)]
	[ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetProfilePictureAsync(Guid id)
	{
		var document = await employeeAppService.GetProfilePictureAsync(id);

		if (document == null)
		{
			return NotFound(new { Message = "Profile picture not found." });
		}

		return File(document.Content, document.ContentType, document.FileName);
	}

	/// <summary>
	/// Upload employee profile picture.
	/// </summary>
	/// <param name="id">Employee ID</param>
	/// <param name="request">Profile picture upload request containing the file</param>
	/// <returns>Result with blob name of uploaded picture</returns>
	[HttpPost("{id}/profile-picture")]
	[Consumes("multipart/form-data")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add)]
	[ProducesResponseType(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> UploadProfilePictureAsync(Guid id, [FromForm] EmployeeProfilePictureUploadRequest request)
	{
		if (request?.File == null || request.File.Length == 0)
		{
			return BadRequest(new { Message = "No file was uploaded." });
		}

		var file = request.File;

		// Validate file type
		var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
		var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

		if (!allowedExtensions.Contains(fileExtension))
		{
			return BadRequest(new { Message = "Invalid file type. Only image files are allowed." });
		}

		// Validate file size (max 5MB)
		if (file.Length > 5 * 1024 * 1024)
		{
			return BadRequest(new { Message = "File size exceeds 5MB limit." });
		}

		await using var memoryStream = new MemoryStream();
		await file.CopyToAsync(memoryStream);

		var blobName = await employeeAppService.UploadProfilePictureAsync(
			id,
			file.FileName,
			memoryStream.ToArray()
		);

		return Ok(new
		{
			Message = "Profile picture uploaded successfully.",
			BlobName = blobName
		});
	}
}


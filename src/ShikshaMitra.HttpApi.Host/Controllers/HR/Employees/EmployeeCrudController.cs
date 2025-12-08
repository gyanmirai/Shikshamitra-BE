using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using System;
using System.IO;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Employees;

/// <summary>
/// Comprehensive employee CRUD operations controller
/// </summary>
[Route("api/employee")]
[Authorize]
public class EmployeeCrudController : AbpController
{
	private readonly IEmployeeAppService _employeeAppService;

	public EmployeeCrudController(IEmployeeAppService employeeAppService)
	{
		_employeeAppService = employeeAppService;
	}

	/// <summary>
	/// Get list of all employees (paginated and sorted)
	/// </summary>
	/// <param name="input">Pagination and sorting parameters</param>
	/// <returns>Paged list of employees</returns>
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View)]
	public virtual Task<PagedResultDto<EmployeeDto>> GetListAsync([FromQuery] PagedAndSortedResultRequestDto input)
	{
		return _employeeAppService.GetListAsync(input);
	}

	/// <summary>
	/// Get a specific employee by ID
	/// </summary>
	/// <param name="id">Employee ID</param>
	/// <returns>Employee details</returns>
	[HttpGet("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View)]
	public virtual Task<EmployeeDto> GetAsync(Guid id)
	{
		return _employeeAppService.GetAsync(id);
	}

	/// <summary>
	/// Create a new employee with basic information
	/// </summary>
	/// <param name="input">Employee basic information</param>
	/// <returns>Created employee</returns>
	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add)]
	public virtual Task<EmployeeDto> CreateAsync([FromBody] CreateEmployeeBasicInfoDto input)
	{
		return _employeeAppService.CreateBasicInfoAsync(input);
	}

	/// <summary>
	/// Update an existing employee's basic information
	/// </summary>
	/// <param name="id">Employee ID</param>
	/// <param name="input">Updated basic information</param>
	/// <returns>Updated employee</returns>
	[HttpPut("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add)]
	public virtual Task<EmployeeDto> UpdateAsync(Guid id, [FromBody] UpdateEmployeeBasicInfoDto input)
	{
		return _employeeAppService.UpdateBasicInfoAsync(id, input);
	}

	/// <summary>
	/// Delete an employee
	/// </summary>
	/// <param name="id">Employee ID</param>
	[HttpDelete("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Delete)]
	public virtual Task DeleteAsync(Guid id)
	{
		return _employeeAppService.DeleteAsync(id);
	}

	/// <summary>
	/// Update employee family information
	/// </summary>
	/// <param name="id">Employee ID</param>
	/// <param name="input">Family details</param>
	/// <returns>Updated employee</returns>
	[HttpPut("{id}/family-info")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add)]
	public virtual Task<EmployeeDto> UpdateFamilyInfoAsync(Guid id, [FromBody] UpdateEmployeeFamilyDetailsDto input)
	{
		return _employeeAppService.UpdateFamilyInfo(id, input);
	}

	/// <summary>
	/// Update employee work experience
	/// </summary>
	/// <param name="id">Employee ID</param>
	/// <param name="input">Work experience details</param>
	/// <returns>Updated employee</returns>
	[HttpPut("{id}/experience")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add)]
	public virtual Task<EmployeeDto> UpdateExperienceInfoAsync(Guid id, [FromBody] System.Collections.Generic.List<CreateUpdateWorkExperienceDto> input)
	{
		return _employeeAppService.UpdateExperienceInfo(id, input);
	}

	/// <summary>
	/// Update employee bank details
	/// </summary>
	/// <param name="id">Employee ID</param>
	/// <param name="input">Bank details</param>
	/// <returns>Updated employee</returns>
	[HttpPut("{id}/bank-details")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add)]
	public virtual Task<EmployeeDto> UpdateBankDetailsAsync(Guid id, [FromBody] BankDetailsDto input)
	{
		return _employeeAppService.UpdateBankDetails(id, input);
	}

	/// <summary>
	/// Update employee status (Active, Inactive, Terminated, etc.)
	/// </summary>
	/// <param name="id">Employee ID</param>
	/// <param name="statusType">New status</param>
	/// <param name="changeReason">Reason for status change</param>
	/// <returns>Updated employee</returns>
	[HttpPut("{id}/status")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add)]
	public virtual Task<EmployeeDto> UpdateStatusAsync(
		Guid id,
		[FromQuery] EmployeeStatusType statusType,
		[FromQuery] string? changeReason = null)
	{
		return _employeeAppService.UpdateEmployeeStatus(id, statusType, changeReason);
	}

	/// <summary>
	/// Get employee profile picture
	/// </summary>
	/// <param name="id">Employee ID</param>
	/// <returns>Profile picture file</returns>
	[HttpGet("{id}/profile-picture")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View)]
	public virtual async Task<IActionResult> GetProfilePictureAsync(Guid id)
	{
		var document = await _employeeAppService.GetProfilePictureAsync(id);

		if (document == null)
		{
			return NotFound(new { Message = "Profile picture not found." });
		}

		return File(document.Content, document.ContentType, document.FileName);
	}

	/// <summary>
	/// Upload employee profile picture
	/// </summary>
	/// <param name="id">Employee ID</param>
	/// <param name="file">Profile picture file</param>
	/// <returns>Blob name of uploaded picture</returns>
	[HttpPost("{id}/profile-picture")]
	[Consumes("multipart/form-data")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add)]
	public virtual async Task<IActionResult> UploadProfilePictureAsync(Guid id, [FromForm] IFormFile file)
	{
		if (file == null || file.Length == 0)
		{
			return BadRequest(new { Message = "No file was uploaded." });
		}

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

		var blobName = await _employeeAppService.UploadProfilePictureAsync(
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

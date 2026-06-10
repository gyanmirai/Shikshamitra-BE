using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees.WorkExperienceContracts;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.HR.Employees.WorkExperienceContracts;

[RemoteService(false)]
[Authorize]
public class EmployeeWorkExperienceAppService(
	IEmployeeRepository employeeRepository,
	EmployeeManager employeeManager,
	IEmployeeAuthorizationService employeeAuthorizationService)
	: ApplicationService, IEmployeeWorkExperienceAppService
{
	/// <inheritdoc />
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View)]
	public async Task<List<WorkExperienceContract>> GetAsync(Guid employeeId)
	{
		await employeeAuthorizationService.CheckEmployeeAccessAsync(employeeId);

		var employee = await employeeRepository.GetAsync(employeeId)
			?? throw new EntityNotFoundException(typeof(Employee), employeeId);

		return employee.WorkExperiences
			.Select(EmployeeWorkExperienceProjection.ToContract)
			.ToList();
	}

	/// <inheritdoc />
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Edit)]
	public async Task UpsertAsync(Guid employeeId, List<WorkExperienceContract> input)
	{
		await employeeAuthorizationService.CheckEmployeeModifyAccessAsync(employeeId);

		var workExperiences = input
			.Select(EmployeeWorkExperienceProjection.ToEntity)
			.ToList();

		var employee = await employeeManager.UpdateExperienceInfoByEmployeeIdAsync(
			employeeId,
			workExperiences);

		await employeeRepository.UpdateAsync(employee);
	}
}

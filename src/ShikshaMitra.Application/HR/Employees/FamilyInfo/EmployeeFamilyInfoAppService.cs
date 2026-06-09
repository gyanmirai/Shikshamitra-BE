using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees.FamilyInfo;

[RemoteService(false)]
[Authorize]
public class EmployeeFamilyInfoAppService(
	IEmployeeRepository employeeRepository,
	IEmployeeFamilyMemberRepository familyMemberRepository,
	EmployeeManager employeeManager)
	: ApplicationService, IEmployeeFamilyInfoAppService
{
	/// <inheritdoc />
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View)]
	public async Task<FamilyInfoResponse> GetAsync(Guid employeeId)
	{
		var employee = await employeeRepository.GetAsync(employeeId);
		var familyMembers = await familyMemberRepository.GetByEmployeeIdAsync(employeeId);

		return EmployeeFamilyInfoProjection.ToResponse(employee.Id, familyMembers);
	}

	/// <inheritdoc />
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Edit)]
	public async Task UpsertAsync(Guid employeeId, FamilyInfoRequest input)
	{
		await employeeManager.UpdateFamilyInfoByEmployeeIdAsync(
			employeeId,
			EmployeeFamilyInfoProjection.ToContactDetails(input.FatherDetails),
			EmployeeFamilyInfoProjection.ToContactDetails(input.MotherDetails),
			EmployeeFamilyInfoProjection.ToContactDetails(input.SpouseDetails));
	}
}


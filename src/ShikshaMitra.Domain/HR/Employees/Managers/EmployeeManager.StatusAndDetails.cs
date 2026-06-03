using ShikshaMitra.Common;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShikshaMitra.HR.Employees;

public partial class EmployeeManager
{
	public async Task<Employee> UpdateEmployeeStatusAsync(
		Guid employeeId,
		EmployeeStatusType employeeStatusType,
		string? changeReason,
		DateOnly? statusEffectiveDate = null)
	{
		var employee = await _employeeLookupReader.GetRequiredAsync(employeeId);

		employee = employee.UpdateEmployeeStatus(
			employeeStatusType,
			changeReason ?? string.Empty,
			statusEffectiveDate,
			new DateTimeOffset(Clock.Now.ToUniversalTime(), TimeSpan.Zero));
		return employee;
	}

	public async Task UpdateFamilyInfoByEmployeeIdAsync(
		Guid employeeId,
		ContactDetails? fatherDetails,
		ContactDetails? motherDetails,
		ContactDetails? spouseDetails)
	{
		await _familyInfoManager.UpdateFamilyInfoByEmployeeIdAsync(employeeId, fatherDetails, motherDetails, spouseDetails);
	}

	public async Task<List<EmployeeFamilyMember>> GetFamilyMembersByEmployeeIdAsync(Guid employeeId)
	{
		return await _familyInfoManager.GetFamilyMembersByEmployeeIdAsync(employeeId);
	}

	public async Task<Employee> UpdateExperienceInfoByEmployeeIdAsync(
		Guid employeeId,
		List<WorkExperience> workExperiences)
	{
		var employee = await _employeeLookupReader.GetRequiredAsync(employeeId);

		employee.UpdateWorkExperiences(workExperiences);
		return employee;
	}

	public async Task<Employee> UpdateBankDetailsByEmployeeIdAsync(
		Guid employeeId,
		BankDetails bankDetails)
	{
		var employee = await _employeeLookupReader.GetRequiredAsync(employeeId);

		employee.UpdateBankDetails(bankDetails);
		return employee;
	}
}

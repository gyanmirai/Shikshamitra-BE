using Microsoft.AspNetCore.Identity;
using ShikshaMitra.Common;
using ShikshaMitra.Groups;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Calendar.HolidayTypes;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Settings;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.ProfessionalManagement;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

public partial class HRSeederContributor
{
	private async Task<Dictionary<string, string>> GenerateSeedEmployeeCodesAsync(
		IReadOnlyCollection<SuratSeedEmployeeProfile> profiles,
		IReadOnlyDictionary<string, SeedRoleInfo> rolesByName,
		IReadOnlyCollection<Employee> existingSeedEmployees)
	{
		var codeByEmail = existingSeedEmployees
			.Where(x => !string.IsNullOrWhiteSpace(x.Email) && !string.IsNullOrWhiteSpace(x.EmployeeCode))
			.ToDictionary(x => x.Email!, x => x.EmployeeCode!, StringComparer.OrdinalIgnoreCase);
		var sequenceByScope = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		var employeeQuery = await _employeeRepository.GetQueryableAsync();
		var existingCodes = employeeQuery
			.Select(x => x.EmployeeCode)
			.Where(x => x != null)
			.AsEnumerable()
			.Select(x => x!)
			.ToList();

		foreach (var profile in profiles.OrderBy(x => x.DateOfJoining).ThenBy(x => x.Email, StringComparer.OrdinalIgnoreCase))
		{
			if (codeByEmail.ContainsKey(profile.Email))
			{
				continue;
			}

			var role = rolesByName[NormalizeRoleName(profile.RoleName)];
			var roleCode = GetSeedRoleCode(role.Name);
			var yearCode = (profile.DateOfJoining.Year % 100).ToString("D2");
			var prefix = $"{roleCode}{yearCode}";
			var scope = $"{prefix}";
			if (!sequenceByScope.TryGetValue(scope, out var current))
			{
				current = existingCodes.Count(code => code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
			}

			current++;
			sequenceByScope[scope] = current;
			codeByEmail[profile.Email] = $"{prefix}{ToBase36(current).PadLeft(4, '0')[^4..].ToUpperInvariant()}";
		}

		return codeByEmail;
	}

	private Employee CreateSeedEmployee(
		SuratSeedEmployeeProfile profile,
		Guid userId,
		Guid roleId,
		string employeeCode,
		Guid? shiftId)
	{
		return new Employee(
			id: userId,
			tenantId: CurrentTenant.Id,
			userId: userId,
			employeeCode: employeeCode,
			basicInfo: BuildSeedEmployeeBasicInfo(profile, roleId, shiftId),
			bankDetails: new BankDetails(null, null, null, null, null),
			workExperiences: BuildWorkExperiences(profile));
	}

	private static void UpdateSeedEmployee(Employee employee, SuratSeedEmployeeProfile profile, Guid roleId, Guid? shiftId)
	{
		employee.UpdateBasicInfo(BuildSeedEmployeeBasicInfo(profile, roleId, shiftId, employee.ProfilePictureBlobName, employee.EmployeeStatusType));
	}

	private static EmployeeBasicInfo BuildSeedEmployeeBasicInfo(
		SuratSeedEmployeeProfile profile,
		Guid roleId,
		Guid? shiftId,
		string? profilePictureBlobName = null,
		EmployeeStatusType? employeeStatusType = null) =>
		new(
			profilePictureBlobName,
			profile.LastName,
			profile.FirstName,
			profile.MiddleName,
			$"{profile.FirstName[0]}.{profile.LastName}",
			employeeStatusType ?? EmployeeStatusType.Active,
			profile.DateOfBirth,
			profile.FamilyCast,
			profile.CastType,
			profile.Gender,
			profile.Religion,
			profile.MaritalStatus,
			profile.BloodGroup,
			profile.Mobile,
			profile.SecondaryMobile,
			profile.Email,
			BuildSeedPanNo(profile.Mobile, profile.Email),
			BuildSeedAdharNo(profile.Mobile),
			profile.Address1,
			profile.Address2,
			profile.Address3,
			profile.Department,
			profile.Designation,
			profile.DateOfJoining,
			profile.EmployeeGroup,
			profile.Qualification,
			roleId,
			profile.IdentificationMark,
			profile.City,
			profile.State,
			profile.Country,
			profile.PinCode,
			shiftId);
}

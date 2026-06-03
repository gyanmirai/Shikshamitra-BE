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
	private async Task EnsureEmployeeOrganizationAssignmentsBatchAsync(
		IReadOnlyDictionary<string, Employee> employeesByEmail,
		IReadOnlyDictionary<string, SuratSeedEmployeeProfile> profilesByEmail)
	{
		var stages = (await _educationMediumStageRepository.GetQueryableAsync())
			.OrderBy(x => x.Name)
			.Select(x => new SeedStageInfo(x.Id, x.Name))
			.ToList();
		if (stages.Count == 0 || employeesByEmail.Count == 0)
		{
			return;
		}

		var employeeIds = employeesByEmail.Values.Select(x => x.Id).ToHashSet();
		var existing = await _employeeOrganizationAssignmentRepository.GetListAsync(x => employeeIds.Contains(x.EmployeeId));
		var existingKeys = existing.Select(x => (x.EmployeeId, x.EducationMediumStageId)).ToHashSet();
		var assignments = new List<EmployeeOrganizationAssignment>();

		foreach (var (email, employee) in employeesByEmail)
		{
			if (!profilesByEmail.TryGetValue(email, out var profile))
			{
				continue;
			}

			foreach (var stageId in ResolveSeedOrganizationIdsForEmployee(profile.RoleName, profile.Email, stages))
			{
				if (!existingKeys.Add((employee.Id, stageId)))
				{
					continue;
				}

				assignments.Add(new EmployeeOrganizationAssignment(
					GuidGenerator.Create(),
					CurrentTenant.Id,
					employee.Id,
					stageId));
			}
		}

		if (assignments.Count > 0)
		{
			await _employeeOrganizationAssignmentRepository.InsertManyAsync(assignments, autoSave: true);
		}

		_logger.LogInformation(
			"[HRSeeder] Employee organization assignments inserted={InsertedCount}",
			assignments.Count);
	}

	private static IEnumerable<(FamilyRelationType RelationType, ContactDetails? Details)> BuildSeedFamilyDetails(
		SuratSeedEmployeeProfile profile)
	{
		yield return (FamilyRelationType.Father, new ContactDetails(
			$"{profile.FirstName}bhai {profile.LastName}",
			profile.EmployeeGroup == EmployeeGroupType.Teaching ? "Retired Teacher" : "Retired Service Professional",
			BuildDemoMobile(profile.Mobile, 7),
			profile.Address1,
			profile.Address2,
			profile.Address3,
			profile.City,
			profile.State.Name,
			profile.State.IsoCode,
			profile.Country.Name,
			profile.Country.IsoCode,
			profile.PinCode));
		yield return (FamilyRelationType.Mother, new ContactDetails(
			$"{profile.FirstName}ben {profile.LastName}",
			"Homemaker",
			BuildDemoMobile(profile.Mobile, 8),
			profile.Address1,
			profile.Address2,
			profile.Address3,
			profile.City,
			profile.State.Name,
			profile.State.IsoCode,
			profile.Country.Name,
			profile.Country.IsoCode,
			profile.PinCode));
		if (profile.MaritalStatus == MaritalStatusType.Married)
		{
			yield return (FamilyRelationType.Spouse, new ContactDetails(
				profile.Gender == GenderType.Male ? $"Mitali {profile.LastName}" : $"Nirav {profile.LastName}",
				"Private Sector Professional",
				BuildDemoMobile(profile.Mobile, 9),
				profile.Address1,
				profile.Address2,
				profile.Address3,
				profile.City,
				profile.State.Name,
				profile.State.IsoCode,
				profile.Country.Name,
				profile.Country.IsoCode,
				profile.PinCode));
		}
	}

	private static IReadOnlyCollection<Guid> ResolveSeedOrganizationIdsForEmployee(
		string roleName,
		string email,
		IReadOnlyList<SeedStageInfo> stages)
	{
		var broadAccessRoles = new[] { "Director", "Principal", "Admin", "Administrative", "Manager", "Clerk", "Finance", "HR" };
		if (broadAccessRoles.Any(token => roleName.Contains(token, StringComparison.OrdinalIgnoreCase)))
		{
			return stages.Select(x => x.Id).ToList();
		}

		var primary = stages.FirstOrDefault(x => x.Name.Contains("Primary", StringComparison.OrdinalIgnoreCase));
		var secondary = stages.FirstOrDefault(x => x.Name.Contains("Secondary", StringComparison.OrdinalIgnoreCase));
		if (roleName.Contains("Teacher", StringComparison.OrdinalIgnoreCase))
		{
			var preferred = StableSeedHash(email) % 2 == 0 ? primary ?? stages[0] : secondary ?? stages[^1];
			return new[] { preferred.Id };
		}

		return new[] { stages[0].Id };
	}

	private static string NormalizeRoleName(string roleName) => roleName.ToUpperInvariant();

	private static string GetSeedRoleCode(string roleName)
	{
		var normalized = roleName.Trim().ToUpperInvariant().Replace(" ", "");
		return normalized.Length >= 3 ? normalized[..3] : normalized.PadRight(3, 'X');
	}

	private static string ToBase36(int value)
	{
		const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
		var result = string.Empty;
		do
		{
			result = chars[value % 36] + result;
			value /= 36;
		} while (value > 0);

		return result;
	}
}

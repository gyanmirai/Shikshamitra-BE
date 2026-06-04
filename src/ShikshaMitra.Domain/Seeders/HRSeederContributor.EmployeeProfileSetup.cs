using ShikshaMitra.Common;
using Microsoft.Extensions.Logging;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShikshaMitra.Seeders;

public partial class HRSeederContributor
{
	private async Task<IReadOnlyDictionary<string, DocumentSetting>> EnsureHrDocumentSettingsAsync()
	{
		var requiredSettings = new[]
		{
			("Aadhaar Card", true),
			("PAN Card", true),
			("Qualification Certificate", true),
			("Experience Letter", false),
			("Joining Letter", true),
			("Bank Proof", true),
			("Address Proof", true),
			("Profile Verification", false)
		};
		var existing = await _documentSettingRepository.GetListAsync();
		var byName = existing.ToDictionary(x => x.DocumentName, StringComparer.OrdinalIgnoreCase);

		foreach (var (name, isRequired) in requiredSettings)
		{
			if (byName.ContainsKey(name))
			{
				continue;
			}

			var setting = new DocumentSetting(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				name,
				documentSize: 5,
				isRequired,
				new List<DocumentType> { DocumentType.Pdf });
			await _documentSettingRepository.InsertAsync(setting, autoSave: false);
			byName[name] = setting;
		}

		await CurrentUnitOfWork.SaveChangesAsync();
		return byName;
	}

	private async Task EnsureSeededEmployeeCompletenessAsync()
	{
		var profilesByEmail = BuildSuratSeedEmployeeProfiles();
		var seedEmails = profilesByEmail.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var rolesByName = await GetSeedRolesByNameAsync();
		var shifts = await _shiftRepository.GetListAsync();
		var fullDayShiftId = shifts.FirstOrDefault(s => s.ShiftName == "Full Day Shift")?.Id
		                     ?? shifts.FirstOrDefault()?.Id;
		var documentSettings = await TimeHrPhaseAsync(
			"Phase 5a document settings",
			EnsureHrDocumentSettingsAsync);
		var employees = await TimeHrPhaseAsync(
			"Phase 5b preload seeded employees",
			async () =>
			{
				var list = new List<Employee>();
				var seededIds = (await _employeeRepository.GetListAsync())
					.Where(x => x.Email != null && seedEmails.Contains(x.Email))
					.Select(x => x.Id)
					.ToList();
				foreach (var employeeId in seededIds)
				{
					var employee = await _employeeRepository.GetWithDetailsAsync(employeeId);
					if (employee != null)
					{
						list.Add(employee);
					}
				}

				return list;
			});
		var employeesByEmail = employees
			.Where(x => !string.IsNullOrWhiteSpace(x.Email))
			.ToDictionary(x => x.Email!, StringComparer.OrdinalIgnoreCase);

		await TimeHrPhaseAsync(
			"Phase 5c employee profile/work/documents",
			async () =>
			{
				foreach (var employee in employees)
				{
					if (employee.Email == null || !profilesByEmail.TryGetValue(employee.Email, out var profile))
					{
						continue;
					}

					var role = rolesByName[NormalizeRoleName(profile.RoleName)];
					UpdateSeedEmployee(employee, profile, role.Id, employee.ShiftId ?? fullDayShiftId);

					if (employee.WorkExperiences.Count == 0)
					{
						employee.UpdateWorkExperiences(BuildWorkExperiences(profile));
					}

					await AddMissingSeedEmployeeDocumentsAsync(employee, profile, documentSettings);
				}

				if (employees.Count > 0)
				{
					await _employeeRepository.UpdateManyAsync(employees, autoSave: false);
					await CurrentUnitOfWork.SaveChangesAsync();
				}
			});

		await TimeHrPhaseAsync(
			"Phase 5d family details",
			() => EnsureEmployeeFamilyDetailsBatchAsync(employeesByEmail, profilesByEmail));
		await TimeHrPhaseAsync(
			"Phase 5e organization assignments",
			() => EnsureEmployeeOrganizationAssignmentsBatchAsync(employeesByEmail, profilesByEmail));

		_logger.LogInformation(
			"[HRSeeder] Seeded employee completeness backfilled employees={EmployeeCount}",
			employees.Count);
	}

	private async Task<Dictionary<string, SeedRoleInfo>> GetSeedRolesByNameAsync()
	{
		_roleSeedInfoByNormalizedName ??= (await _identityRoleRepository.GetQueryableAsync())
			.Select(role => new SeedRoleInfo(role.Id, role.Name))
			.AsEnumerable()
			.Where(role => !string.IsNullOrWhiteSpace(role.Name))
			.ToDictionary(
				role => _lookupNormalizer.NormalizeName(role.Name),
				role => role,
				StringComparer.OrdinalIgnoreCase);

		return _roleSeedInfoByNormalizedName;
	}

	private static void ValidateSeedProfiles(
		IReadOnlyCollection<SuratSeedEmployeeProfile> profiles,
		IReadOnlyDictionary<string, SeedRoleInfo> rolesByName)
	{
		var duplicateEmails = profiles
			.GroupBy(x => x.Email, StringComparer.OrdinalIgnoreCase)
			.Where(x => x.Count() > 1)
			.Select(x => x.Key)
			.ToList();
		if (duplicateEmails.Count > 0)
		{
			throw new InvalidOperationException($"Duplicate seeded employee emails: {string.Join(", ", duplicateEmails)}");
		}

		foreach (var profile in profiles)
		{
			if (!rolesByName.ContainsKey(NormalizeRoleName(profile.RoleName)))
			{
				throw new InvalidOperationException(
					$"Seeded employee role '{profile.RoleName}' was not found for {profile.Email}.");
			}

			if (profile.DateOfJoining <= profile.DateOfBirth)
			{
				throw new InvalidOperationException(
					$"Seeded employee '{profile.Email}' has DateOfJoining before DateOfBirth.");
			}

			if (string.IsNullOrWhiteSpace(profile.Mobile) || profile.Mobile.Length < 4)
			{
				throw new InvalidOperationException($"Seeded employee '{profile.Email}' has invalid mobile number.");
			}
		}
	}
}

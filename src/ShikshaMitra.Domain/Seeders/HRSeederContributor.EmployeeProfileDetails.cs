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
	private static readonly string[] RequiredSeedEmployeeDocumentNames =
	[
		"Aadhaar Card",
		"PAN Card",
		"Qualification Certificate",
		"Experience Letter",
		"Joining Letter",
		"Bank Proof",
		"Address Proof",
		"Profile Verification"
	];

	private async Task EnsureEmployeeFamilyDetailsAsync(Guid employeeId, SuratSeedEmployeeProfile profile)
	{
		await _employeeManager.UpdateFamilyInfoByEmployeeIdAsync(
			employeeId,
			new ContactDetails(
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
				profile.PinCode),
			new ContactDetails(
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
				profile.PinCode),
			profile.MaritalStatus == MaritalStatusType.Married
				? new ContactDetails(
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
					profile.PinCode)
				: null);
	}

	private async Task EnsureEmployeeWorkExperiencesAsync(Guid employeeId, SuratSeedEmployeeProfile profile)
	{
		var employee = await _employeeRepository.GetAsync(employeeId);
		if (employee.WorkExperiences.Count > 0)
		{
			return;
		}

		var experiences = BuildWorkExperiences(profile);
		await _employeeManager.UpdateExperienceInfoByEmployeeIdAsync(employeeId, experiences);
	}

	private async Task EnsureEmployeeDocumentsAsync(
		Guid employeeId,
		SuratSeedEmployeeProfile profile,
		IReadOnlyDictionary<string, DocumentSetting> documentSettings)
	{
		var existingDocuments = await _employeeDocumentManager.GetDocumentsAsync(employeeId);
		var existingSettingIds = existingDocuments
			.Where(x => x.DocumentNameId.HasValue)
			.Select(x => x.DocumentNameId!.Value)
			.ToHashSet();
		var documentsToAdd = new List<EmployeeDocumentData>();
		var requiredDocumentNames = new[]
		{
			"Aadhaar Card",
			"PAN Card",
			"Qualification Certificate",
			"Experience Letter",
			"Joining Letter",
			"Bank Proof",
			"Address Proof",
			"Profile Verification"
		};

		foreach (var documentName in requiredDocumentNames)
		{
			if (!documentSettings.TryGetValue(documentName, out var setting) ||
			    existingSettingIds.Contains(setting.Id) ||
			    existingDocuments.Any(x => x.DocumentName.Contains(documentName, StringComparison.OrdinalIgnoreCase)))
			{
				continue;
			}

			var fileName = $"{SanitizeFileToken(profile.Email)}-{SanitizeFileToken(documentName)}.pdf";
			var content = BuildSeedPdfBytes(documentName, profile);
			documentsToAdd.Add(new EmployeeDocumentData
			{
				DocumentNameId = setting.Id,
				FileName = fileName,
				ContentType = "application/pdf",
				Content = content,
				Size = content.LongLength
			});
		}

		if (documentsToAdd.Count > 0)
		{
			await _employeeDocumentManager.AddDocumentsAsync(employeeId, documentsToAdd);
		}
	}
}

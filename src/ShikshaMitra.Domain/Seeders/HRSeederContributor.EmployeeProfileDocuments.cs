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
	private async Task AddMissingSeedEmployeeDocumentsAsync(
		Employee employee,
		SuratSeedEmployeeProfile profile,
		IReadOnlyDictionary<string, DocumentSetting> documentSettings)
	{
		var existingSettingIds = employee.Documents
			.Where(x => x.DocumentNameId.HasValue)
			.Select(x => x.DocumentNameId!.Value)
			.ToHashSet();
		var documentsToAdd = new List<EmployeeDocumentData>();
		foreach (var documentName in RequiredSeedEmployeeDocumentNames)
		{
			if (!documentSettings.TryGetValue(documentName, out var setting) ||
			    existingSettingIds.Contains(setting.Id) ||
			    employee.Documents.Any(x => x.DocumentName.Contains(documentName, StringComparison.OrdinalIgnoreCase)))
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
			await _employeeDocumentManager.AddDocumentsAsync(employee, documentsToAdd, updateEmployee: false);
		}
	}

	private async Task EnsureEmployeeFamilyDetailsBatchAsync(
		IReadOnlyDictionary<string, Employee> employeesByEmail,
		IReadOnlyDictionary<string, SuratSeedEmployeeProfile> profilesByEmail)
	{
		var employeeIds = employeesByEmail.Values.Select(x => x.Id).ToHashSet();
		var existing = await _familyMemberRepository.GetListAsync(x => employeeIds.Contains(x.EmployeeId));
		var existingByKey = existing.ToDictionary(x => (x.EmployeeId, x.RelationType));
		var toInsert = new List<EmployeeFamilyMember>();
		var toUpdate = new List<EmployeeFamilyMember>();

		foreach (var (email, employee) in employeesByEmail)
		{
			if (!profilesByEmail.TryGetValue(email, out var profile))
			{
				continue;
			}

			foreach (var (relationType, details) in BuildSeedFamilyDetails(profile))
			{
				if (details == null || string.IsNullOrWhiteSpace(details.Name))
				{
					continue;
				}

				if (existingByKey.TryGetValue((employee.Id, relationType), out var existingMember))
				{
					existingMember.Update(
						details.Name,
						details.Occupation,
						details.ContactNo,
						details.Address1,
						details.Address2,
						details.Address3,
						details.City,
						details.StateName,
						details.StateIsoCode,
						details.CountryName,
						details.CountryIsoCode,
						details.PinCode);
					toUpdate.Add(existingMember);
				}
				else
				{
					toInsert.Add(new EmployeeFamilyMember(
						GuidGenerator.Create(),
						CurrentTenant.Id,
						employee.Id,
						relationType,
						details.Name,
						details.Occupation,
						details.ContactNo,
						details.Address1,
						details.Address2,
						details.Address3,
						details.City,
						details.StateName,
						details.StateIsoCode,
						details.CountryName,
						details.CountryIsoCode,
						details.PinCode));
				}
			}
		}

		if (toInsert.Count > 0)
		{
			await _familyMemberRepository.InsertManyAsync(toInsert, autoSave: false);
		}

		if (toUpdate.Count > 0)
		{
			await _familyMemberRepository.UpdateManyAsync(toUpdate, autoSave: false);
		}

		if (toInsert.Count > 0 || toUpdate.Count > 0)
		{
			await CurrentUnitOfWork.SaveChangesAsync();
		}

		_logger.LogInformation(
			"[HRSeeder] Employee family details ensured inserted={InsertedCount}, updated={UpdatedCount}",
			toInsert.Count,
			toUpdate.Count);
	}
}

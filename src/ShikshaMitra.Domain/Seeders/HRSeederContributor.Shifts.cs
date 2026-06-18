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
	private const string SeedDefaultShiftName = "Full Day Shift";

	private async Task SeedShiftManagementAsync()
	{
		var existingShifts = await _shiftRepository.GetListAsync();
		var existingByName = existingShifts
			.Where(shift => !string.IsNullOrWhiteSpace(shift.ShiftName))
			.GroupBy(shift => shift.ShiftName, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
		var changed = false;

		foreach (var shift in BuildSeedShiftSpecs())
		{
			if (existingByName.TryGetValue(shift.Name, out var existingShift))
			{
				if (!string.Equals(existingShift.ShiftName, shift.Name, StringComparison.Ordinal) ||
				    existingShift.StartTime != shift.StartTime ||
				    existingShift.EndTime != shift.EndTime)
				{
					existingShift.ShiftName = shift.Name;
					existingShift.StartTime = shift.StartTime;
					existingShift.EndTime = shift.EndTime;
					await _shiftRepository.UpdateAsync(existingShift, autoSave: false);
					changed = true;
				}

				continue;
			}

			var id = GuidGenerator.Create();
			var shiftEntity = new ShiftManagement
			{
				ShiftName = shift.Name,
				StartTime = shift.StartTime,
				EndTime = shift.EndTime
			};
			// Set the Id using reflection since there's no constructor
			var idProperty = typeof(ShiftManagement).GetProperty(nameof(ShiftManagement.Id))
				?? throw new InvalidOperationException("ShiftManagement.Id property was not found.");
			idProperty.SetValue(shiftEntity, id);
			await _shiftRepository.InsertAsync(shiftEntity, autoSave: false);
			changed = true;
		}

		if (changed)
		{
			await CurrentUnitOfWork.SaveChangesAsync();
		}
	}

	internal static IReadOnlyList<SeedShiftSpec> BuildSeedShiftSpecs() =>
	[
		new("Morning Shift", new TimeOnly(7, 0), new TimeOnly(12, 30)),
		new("Afternoon Shift", new TimeOnly(12, 30), new TimeOnly(17, 30)),
		new(SeedDefaultShiftName, new TimeOnly(7, 0), new TimeOnly(17, 30))
	];

	internal static Guid? ResolveSeedDefaultShiftId(IEnumerable<ShiftManagement> shifts)
	{
		var shiftList = shifts.ToList();
		return shiftList
			.FirstOrDefault(shift => string.Equals(shift.ShiftName, SeedDefaultShiftName, StringComparison.OrdinalIgnoreCase))
			?.Id ?? shiftList.FirstOrDefault()?.Id;
	}

	internal sealed record SeedShiftSpec(string Name, TimeOnly StartTime, TimeOnly EndTime);
}

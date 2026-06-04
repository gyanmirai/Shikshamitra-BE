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
	private async Task SeedPayrollSettingsAsync()
	{
		if (!await _providentFundRepository.IsCodeExistsAsync("STD-PF"))
		{
			await _providentFundRepository.InsertAsync(
				new ProvidentFund(
					GuidGenerator.Create(),
					CurrentTenant.Id,
					"STD-PF",
					"Standard Provident Fund",
					employeePercentage: 12,
					employerPercentage: 12),
				autoSave: true);
		}

		var professionalTaxSlabs = new[]
		{
			new { Gender = GenderType.Male, From = 0d, To = 5_999.99d, Amount = 0d },
			new { Gender = GenderType.Male, From = 6_000d, To = 8_999.99d, Amount = 80d },
			new { Gender = GenderType.Male, From = 9_000d, To = 11_999.99d, Amount = 150d },
			new { Gender = GenderType.Male, From = 12_000d, To = 999_999d, Amount = 200d },
			new { Gender = GenderType.Female, From = 0d, To = 5_999.99d, Amount = 0d },
			new { Gender = GenderType.Female, From = 6_000d, To = 8_999.99d, Amount = 80d },
			new { Gender = GenderType.Female, From = 9_000d, To = 11_999.99d, Amount = 150d },
			new { Gender = GenderType.Female, From = 12_000d, To = 999_999d, Amount = 200d }
		};

		foreach (var slab in professionalTaxSlabs)
		{
			if (await _professionalTaxRepository.AnyAsync(x =>
				    x.GenderType == slab.Gender &&
				    x.FromSalary == slab.From &&
				    x.ToSalary == slab.To))
			{
				continue;
			}

			await _professionalTaxRepository.InsertAsync(
				new ProfessionalTax(
					GuidGenerator.Create(),
					CurrentTenant.Id,
					slab.Gender,
					slab.From,
					slab.To,
					slab.Amount),
				autoSave: false);
		}

		await SeedDefaultHrPoliciesAsync();

		await CurrentUnitOfWork.SaveChangesAsync();
	}
}

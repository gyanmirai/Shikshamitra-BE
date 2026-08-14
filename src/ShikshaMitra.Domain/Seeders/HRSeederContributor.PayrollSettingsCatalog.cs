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
using ShikshaMitra.HR.Payroll.Templates;
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
					employerPercentage: 12,
					contributionBasis: PfContributionBasis.Basic,
					wageCeiling: 15_000m,
					effectiveFrom: new DateTime(2022, 4, 1)),
				autoSave: true);
		}

		var professionalTaxSlabs = new[]
		{
			new { From = 0m, To = (decimal?)12_000m, Amount = 0m },
			new { From = 12_000.01m, To = (decimal?)null, Amount = 200m }
		};

		foreach (var slab in professionalTaxSlabs)
		{
			if (await _professionalTaxRepository.AnyAsync(x =>
				    x.JurisdictionCode == "IN-GJ" &&
				    x.EffectiveFrom == new DateTime(2022, 4, 1) &&
				    x.FromSalary == slab.From &&
				    x.ToSalary == slab.To))
			{
				continue;
			}

			await _professionalTaxRepository.InsertAsync(
				new ProfessionalTax(
					GuidGenerator.Create(),
					CurrentTenant.Id,
					"IN-GJ",
					new DateTime(2022, 4, 1),
					null,
					slab.From,
					slab.To,
					slab.Amount),
				autoSave: false);
		}

		await CurrentUnitOfWork.SaveChangesAsync();
	}
}

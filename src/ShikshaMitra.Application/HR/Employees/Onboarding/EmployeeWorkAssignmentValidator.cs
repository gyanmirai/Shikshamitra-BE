using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.AcademicSetups.AcademicOrganizationOptions;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Validation;

namespace ShikshaMitra.HR.Employees.Onboarding;

[RemoteService(false)]
public class EmployeeWorkAssignmentValidator(
	IAcademicOrganizationOptionsAppService organizationOptions,
	IAcademicYearRepository academicYears) : ApplicationService, ITransientDependency
{
	public async Task<EmployeeWorkAssignmentSummaryDto> ValidateAsync(EmployeeWorkAssignmentDto input)
	{
		if (input.Mode is not (EmployeeWorkAssignmentModes.SpecificOrganizations or
			EmployeeWorkAssignmentModes.SchoolWide or EmployeeWorkAssignmentModes.DecideLater))
			throw new AbpValidationException("Choose a valid organization / work assignment.");
		if (input.OrganizationIds == null || input.OrganizationIds.Count > 100 ||
			input.OrganizationIds.Contains(Guid.Empty) ||
			input.OrganizationIds.Distinct().Count() != input.OrganizationIds.Count)
			throw new AbpValidationException("Choose unique, valid work assignment organizations.");
		if (input.Mode != EmployeeWorkAssignmentModes.SpecificOrganizations)
		{
			if (input.AcademicYearId.HasValue || input.OrganizationIds.Count != 0)
				throw new AbpValidationException("School-wide and decide-later assignments cannot include organizations or an academic year.");
			return new EmployeeWorkAssignmentSummaryDto { Mode = input.Mode };
		}
		if (!input.AcademicYearId.HasValue || input.AcademicYearId == Guid.Empty || input.OrganizationIds.Count == 0)
			throw new AbpValidationException("Choose an academic year and at least one work assignment organization.");
		var year = await academicYears.FindAsync(input.AcademicYearId.Value);
		if (year == null || year.TenantId != CurrentTenant.Id)
			throw new AbpValidationException("The work assignment academic year is no longer available.");
		// Reuse the same tenant/year/actor-scoped catalogue as the picker. No access is assigned here.
		var options = await organizationOptions.GetListAsync(new AcademicOrganizationOptionsInput
		{
			AcademicYearId = year.Id
		});
		var byId = options.Items.ToDictionary(x => x.Id);
		if (input.OrganizationIds.Any(id => !byId.ContainsKey(id)))
			throw new AbpValidationException("A work assignment organization is no longer available in this academic year or your scope. Review the assignment.");
		return new EmployeeWorkAssignmentSummaryDto
		{
			Mode = input.Mode,
			AcademicYearId = year.Id,
			AcademicYearName = year.Name,
			OrganizationIds = input.OrganizationIds.ToList(),
			OrganizationLabels = input.OrganizationIds.Select(id => byId[id].Label).ToList()
		};
	}
}

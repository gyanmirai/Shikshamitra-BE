using ShikshaMitra.Extensions;
using ShikshaMitra.Onboarding.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.Divisions;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace ShikshaMitra.TenantSettings.AcademicSetups;

public class DivisionManager(IDivisionRepository divisionRepository, IGuidGenerator guidGenerator) : DomainService
{
	private string EntityName => this.GetType().ToEntityNameFromManager();

	public async Task<Division> CreateAsync(
		Guid standardId,
		string name,
		int strength)
	{
		// Validate the name is unique within the same Standard.
		await ValidateDivisionAsync(name, standardId);

		return new Division(
			guidGenerator.Create(),
			tenantId: CurrentTenant.Id,
			name,
			standardId,
			strength,
			"101"
		);
	}

	public async Task<Division> UpdateAsync(
		Division division,
		string newName,
		int newStrength)
	{
		Check.NotNull(division, nameof(division));

		if (division.Name == newName && division.Strength == newStrength)
		{
			return division;
		}

		// Validate the name is unique within the same Standard.
		await ValidateDivisionAsync(newName, division.StandardId);

		division.SetName(newName);
		division.SetStrength(newStrength);

		return division;
	}

	private async Task ValidateDivisionAsync(string name, Guid standardId)
	{
		if (await divisionRepository.FindByNameAndStandardIdAsync(name, standardId) != null)
		{
			throw new AlreadyExistsException(EntityName, name);
		}
	}
}
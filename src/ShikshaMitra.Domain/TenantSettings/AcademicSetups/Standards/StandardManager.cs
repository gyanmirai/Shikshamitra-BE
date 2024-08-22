using ShikshaMitra.Extensions;
using ShikshaMitra.Onboarding.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace ShikshaMitra.TenantSettings.AcademicSetups;

public class StandardManager(IStandardRepository standardRepository, IGuidGenerator guidGenerator) : DomainService
{
	private string EntityName => this.GetType().ToEntityNameFromManager();

	public async Task<Standard> CreateAsync(Guid educationMediumStageId, string name)
	{
		await ValidateStandardAsync(name, educationMediumStageId);

		return new Standard(
			guidGenerator.Create(),
			tenantId: CurrentTenant.Id,
			name,
			educationMediumStageId
		);
	}

	public async Task<Standard> UpdateAsync(Standard standard, string newName)
	{
		Check.NotNull(standard, nameof(standard));

		if (standard.Name == newName)
		{
			return standard;
		}

		await ValidateStandardAsync(newName, standard.EducationMediumStageId);

		standard.SetName(newName);

		return standard;
	}

	private async Task ValidateStandardAsync(string name, Guid educationMediumStageId)
	{
		if (await standardRepository.FindByNameAndEducationMediumStageIdAsync(name, educationMediumStageId) != null)
		{
			throw new AlreadyExistsException(EntityName, name);
		}
	}
}
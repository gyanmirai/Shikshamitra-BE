using ShikshaMitra.Extensions;
using ShikshaMitra.Onboarding.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.EducationMediums;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.TenantSettings.AcademicSetups;

public class EducationMediumManager(IEducationMediumRepository educationMediumRepository) : DomainService
{
	private string EntityName => this.GetType().ToEntityNameFromManager();

	public async Task<EducationMedium> CreateAsync(Guid educationBoardId, string name)
	{
		await ValidateEducationMediumAsync(name, educationBoardId);

		return new EducationMedium(
			GuidGenerator.Create(),
			tenantId: CurrentTenant.Id,
			name,
			educationBoardId
		);
	}

	public async Task<EducationMedium> UpdateAsync(EducationMedium educationMedium, string newName)
	{
		Check.NotNull(educationMedium, nameof(educationMedium));

		if (educationMedium.Name == newName)
		{
			return educationMedium;
		}

		await ValidateEducationMediumAsync(newName, educationMedium.EducationBoardId);

		educationMedium.SetName(newName);

		return educationMedium;
	}

	private async Task ValidateEducationMediumAsync(string name, Guid educationBoardId)
	{
		if (await educationMediumRepository.FindByNameAndEducationBoardIdAsync(name, educationBoardId) != null)
		{
			throw new AlreadyExistsException(EntityName, name);
		}
	}
}
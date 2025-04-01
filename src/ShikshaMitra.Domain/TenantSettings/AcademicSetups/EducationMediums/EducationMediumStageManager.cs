using ShikshaMitra.Extensions;
using ShikshaMitra.Onboarding.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.EducationBoards;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.TenantSettings.AcademicSetups;

public class EducationMediumStageManager(IEducationMediumStageRepository educationMediumStageRepository) : DomainService
{
	private string EntityName => this.GetType().ToEntityNameFromManager();

	public async Task<EducationMediumStage> CreateAsync(Guid educationMediumId, string name)
	{
		await ValidateEducationMediumStageAsync(name, educationMediumId);

		return new EducationMediumStage(
			GuidGenerator.Create(),
			tenantId: CurrentTenant.Id,
			name,
			null,
			educationMediumId
		);
	}

	public async Task<EducationMediumStage> UpdateAsync(EducationMediumStage educationMediumStage, string newName)
	{
		Check.NotNull(educationMediumStage, nameof(educationMediumStage));

		if (educationMediumStage.Name == newName)
		{
			return educationMediumStage;
		}

		await ValidateEducationMediumStageAsync(newName, educationMediumStage.EducationMediumId);

		educationMediumStage.SetName(newName);

		return educationMediumStage;
	}

	private async Task ValidateEducationMediumStageAsync(string name, Guid educationMediumId)
	{
		if (await educationMediumStageRepository.FindByNameAndEducationMediumIdAsync(name, educationMediumId) != null)
		{
			throw new AlreadyExistsException(EntityName, name);
		}
	}
}
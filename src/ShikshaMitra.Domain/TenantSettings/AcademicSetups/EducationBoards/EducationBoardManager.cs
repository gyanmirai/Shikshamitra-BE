using ShikshaMitra.Extensions;
using ShikshaMitra.Onboarding.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.EducationBoards;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.TenantSettings.AcademicSetups;

/// <summary>
/// Manages operations related to the EducationBoard entity, including creating and updating education boards.
/// </summary>
public class EducationBoardManager(IEducationBoardRepository educationBoardRepository) : DomainService
{
	/// <summary>
	/// Gets the name of the entity managed by this class.
	/// </summary>
	private string EntityName => this.GetType().ToEntityNameFromManager();

	/// <summary>
	/// Creates a new EducationBoard entity.
	/// </summary>
	/// <param name="academicYearId">The ID of the academic year associated with the education board.</param>
	/// <param name="educationBoardType">The type of the education board.</param>
	/// <returns>The created <see cref="EducationBoard"/> entity.</returns>
	/// <exception cref="AlreadyExistsException">Thrown if an education board with the specified name already exists.</exception>
	public async Task<EducationBoard> CreateAsync(
		Guid academicYearId,
		EducationBoardType educationBoardType)
	{
		// Validate the name is unique within the same academic year.
		await ValidateEducationBoardAsync(educationBoardType, academicYearId);

		return new EducationBoard(
			GuidGenerator.Create(),
			tenantId: CurrentTenant.Id,
			educationBoardType,
			academicYearId
		);
	}

	/// <summary>
	/// Updates the name of an existing EducationBoard entity.
	/// </summary>
	/// <param name="educationBoard">The education board to update.</param>
	/// <param name="newEducationBoardType">The type of the education board.</param>
	/// <returns>The updated <see cref="EducationBoard"/> entity.</returns>
	/// <exception cref="AlreadyExistsException">Thrown if an education board with the new name already exists.</exception>
	public async Task<EducationBoard> UpdateAsync(
		EducationBoard educationBoard,
		EducationBoardType newEducationBoardType)
	{
		Check.NotNull(educationBoard, nameof(educationBoard));

		// If the name is the same as the current one, return the existing entity without changes.
		if (educationBoard.Type == newEducationBoardType)
		if (educationBoard.Type == newEducationBoardType)
		{
			return educationBoard;
		}
		// Validate the name is unique within the same academic year.
		await ValidateEducationBoardAsync(newEducationBoardType, educationBoard.AcademicYearId);

		// Update the name of the education board.
		educationBoard.SetEducationBoardType(newEducationBoardType);

		return educationBoard;
	}

	/// <summary>
	/// Ensures the name is unique within the specified academic year.
	/// </summary>
	/// <param name="educationBoardType">The education board type to validate.</param>
	/// <param name="academicYearId">The ID of the academic year.</param>
	/// <exception cref="AlreadyExistsException">Thrown if an education board with the specified name already exists.</exception>
	private async Task ValidateEducationBoardAsync(EducationBoardType educationBoardType, Guid academicYearId)
	{
		if (await educationBoardRepository.FindByNameAndAcademicYearIdAsync(educationBoardType, academicYearId) != null)
		{
			throw new AlreadyExistsException(EntityName, educationBoardType.ToString());
		}
	}
}
using ShikshaMitra.Extensions;
using ShikshaMitra.Onboarding.AcademicYears;
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
	/// <param name="name">The name of the education board.</param>
	/// <returns>The created <see cref="EducationBoard"/> entity.</returns>
	/// <exception cref="AlreadyExistsException">Thrown if an education board with the specified name already exists.</exception>
	public async Task<EducationBoard> CreateAsync(
		Guid academicYearId,
		string name)
	{
		// Validate the name is unique within the same academic year.
		await ValidateEducationBoardAsync(name, academicYearId);

		return new EducationBoard(
			GuidGenerator.Create(),
			tenantId: CurrentTenant.Id,
			name,
			academicYearId
		);
	}

	/// <summary>
	/// Updates the name of an existing EducationBoard entity.
	/// </summary>
	/// <param name="educationBoard">The education board to update.</param>
	/// <param name="newName">The new name to assign to the education board.</param>
	/// <returns>The updated <see cref="EducationBoard"/> entity.</returns>
	/// <exception cref="AlreadyExistsException">Thrown if an education board with the new name already exists.</exception>
	public async Task<EducationBoard> UpdateAsync(
		EducationBoard educationBoard,
		string newName)
	{
		Check.NotNull(educationBoard, nameof(educationBoard));

		// If the name is the same as the current one, return the existing entity without changes.
		if (educationBoard.Name == newName)
		{
			return educationBoard;
		}

		// Validate the name is unique within the same academic year.
		await ValidateEducationBoardAsync(newName, educationBoard.AcademicYearId);

		// Update the name of the education board.
		educationBoard.SetName(newName);

		return educationBoard;
	}

	/// <summary>
	/// Ensures the name is unique within the specified academic year.
	/// </summary>
	/// <param name="name">The name of the education board.</param>
	/// <param name="academicYearId">The ID of the academic year.</param>
	/// <exception cref="AlreadyExistsException">Thrown if an education board with the specified name already exists.</exception>
	private async Task ValidateEducationBoardAsync(string name, Guid academicYearId)
	{
		if (await educationBoardRepository.FindByNameAndAcademicYearIdAsync(name, academicYearId) != null)
		{
			throw new AlreadyExistsException(EntityName, name);
		}
	}
}
using ShikshaMitra.Extensions;
using ShikshaMitra.Onboarding.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicYears;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace ShikshaMitra.TenantSettings.AcademicSetups;

/// <summary>
/// This abstract class manages the creation and updating of academic years.
/// </summary>
public class AcademicYearManager(IAcademicYearRepository academicYearRepository,IGuidGenerator guidGenerator) : DomainService
{
	private string EntityName => this.GetType().ToEntityNameFromManager();

	/// <summary>
	/// Creates a new academic year.
	/// </summary>
	/// <param name="name">The name of the academic year.</param>
	/// <param name="startDate">The start date of the academic year.</param>
	/// <param name="endDate">The end date of the academic year.</param>
	/// <returns>The created <see cref="AcademicYear"/>.</returns>
	/// <exception cref="AlreadyExistsException">Thrown if an academic year with the same name already exists.</exception>
	/// <exception cref="InvalidAcademicYearDatesException">Thrown if the end date is before the start date.</exception>
	public async Task<AcademicYear> CreateAsync(
		string name,
		DateTime startDate,
		DateTime endDate)
	{
		Check.NotNullOrWhiteSpace(name, nameof(name));

		// Validate the academic year details.
		await ValidateAcademicYearAsync(name, startDate, endDate);

		// Create and return a new AcademicYear object.
		return new AcademicYear(
			id: guidGenerator.Create(),
			tenantId: CurrentTenant.Id,
			name: name,
			startDate: startDate,
			endDate: endDate);
	}

	/// <summary>
	/// Updates an existing academic year.
	/// </summary>
	/// <param name="academicYear">The academic year to update.</param>
	/// <param name="newName">The new name of the academic year.</param>
	/// <param name="startDate">The start date of the academic year.</param>
	/// <param name="endDate">The end date of the academic year.</param>
	/// <returns>The updated <see cref="AcademicYear"/>.</returns>
	public async Task<AcademicYear> UpdateAsync(
		AcademicYear academicYear,
		string newName,
		DateTime startDate,
		DateTime endDate)
	{
		Check.NotNull(academicYear, nameof(academicYear));
		Check.NotNullOrWhiteSpace(newName, nameof(newName));

		// Validate the academic year details.
		await ValidateAcademicYearAsync(newName, startDate, endDate, academicYear.Name);

		// Change the name if it's different from the current one.
		if (academicYear.Name != newName)
		{
			academicYear.SetName(newName);
		}

		// Change the dates if they are different from the current ones.
		if (academicYear.StartDate != startDate || academicYear.EndDate != endDate)
		{
			academicYear.SetDates(startDate, endDate);
		}

		return academicYear;
	}

	/// <summary>
	/// Validates the academic year details to ensure uniqueness and correct dates.
	/// </summary>
	/// <param name="name">The name of the academic year.</param>
	/// <param name="startDate">The start date of the academic year.</param>
	/// <param name="endDate">The end date of the academic year.</param>
	/// <param name="currentName">The current name of the academic year, if any (optional).</param>
	/// <exception cref="AlreadyExistsException">Thrown if an academic year with the same name already exists.</exception>
	/// <exception cref="InvalidAcademicYearDatesException">Thrown if the end date is before the start date.</exception>
	private async Task ValidateAcademicYearAsync(string name, DateTime startDate, DateTime endDate, string currentName = null)
	{
		// Check if the name is unique (if it's different from the current name).
		if (currentName != name && await academicYearRepository.FindByNameAsync(name) != null)
		{
			throw new AlreadyExistsException(EntityName, name);
		}
	}
}

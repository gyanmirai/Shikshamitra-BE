using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.HR.Employees.AdditionalDetails;

/// <summary>
/// Application service for managing employee additional details.
/// </summary>
[RemoteService(false)]
public class EmployeeAdditionalDetailsAppService(
	IEmployeeRepository employeeRepository,
	IEmployeeAdditionalDetailsRepository additionalDetailsRepository)
	: ApplicationService, IEmployeeAdditionalDetailsAppService
{
	/// <inheritdoc />
	public async Task<EmployeeAdditionalDetailsDto> GetAsync(Guid employeeId)
	{
		// Verify employee exists
		var employee = await employeeRepository.FindAsync(employeeId);
		if (employee == null)
		{
			throw new EntityNotFoundException(typeof(Employee), employeeId);
		}

		// Get additional details
		var additionalDetails = await additionalDetailsRepository.GetByEmployeeIdAsync(employeeId);
		if (additionalDetails == null)
		{
			throw new EntityNotFoundException(typeof(EmployeeAdditionalDetails), employeeId);
		}

		return MapToDto(additionalDetails);
	}

	/// <inheritdoc />
	public async Task<(EmployeeAdditionalDetailsDto Result, bool IsCreated)> UpsertAsync(
		Guid employeeId,
		EmployeeAdditionalDetailsUpsertDto input)
	{
		// Verify employee exists
		var employee = await employeeRepository.FindAsync(employeeId);
		if (employee == null)
		{
			throw new EntityNotFoundException(typeof(Employee), employeeId);
		}

		// Check if additional details already exist
		var existingDetails = await additionalDetailsRepository.GetByEmployeeIdAsync(employeeId);

		EmployeeAdditionalDetails additionalDetails;
		bool isCreated;

		if (existingDetails == null)
		{
			// Create new additional details
			additionalDetails = new EmployeeAdditionalDetails(
				id: GuidGenerator.Create(),
				tenantId: CurrentTenant.Id,
				employeeId: employeeId,
				bplCardNo: input.BplCardNo,
				personWithDisability: input.PersonWithDisability,
				secondLanguage: input.SecondLanguage,
				libraryMember: input.LibraryMember,
				transportMember: input.TransportMember);

			await additionalDetailsRepository.InsertAsync(additionalDetails);
			isCreated = true;
		}
		else
		{
			// Update existing additional details
			existingDetails.Update(
				bplCardNo: input.BplCardNo,
				personWithDisability: input.PersonWithDisability,
				secondLanguage: input.SecondLanguage,
				libraryMember: input.LibraryMember,
				transportMember: input.TransportMember);

			await additionalDetailsRepository.UpdateAsync(existingDetails);
			additionalDetails = existingDetails;
			isCreated = false;
		}

		return (MapToDto(additionalDetails), isCreated);
	}

	/// <inheritdoc />
	public async Task<EmployeeAdditionalDetailsDto> PatchAsync(
		Guid employeeId,
		EmployeeAdditionalDetailsPatchDto input)
	{
		// Verify employee exists
		var employee = await employeeRepository.FindAsync(employeeId);
		if (employee == null)
		{
			throw new EntityNotFoundException(typeof(Employee), employeeId);
		}

		// Get existing additional details
		var additionalDetails = await additionalDetailsRepository.GetByEmployeeIdAsync(employeeId);
		if (additionalDetails == null)
		{
			throw new EntityNotFoundException(typeof(EmployeeAdditionalDetails), employeeId);
		}

		// Apply partial updates
		additionalDetails.Patch(
			bplCardNo: input.BplCardNo,
			personWithDisability: input.PersonWithDisability,
			secondLanguage: input.SecondLanguage,
			libraryMember: input.LibraryMember,
			transportMember: input.TransportMember,
			bplCardNoProvided: input.BplCardNoProvided);

		await additionalDetailsRepository.UpdateAsync(additionalDetails);

		return MapToDto(additionalDetails);
	}

	private static EmployeeAdditionalDetailsDto MapToDto(EmployeeAdditionalDetails entity)
	{
		return new EmployeeAdditionalDetailsDto
		{
			Id = entity.Id,
			EmployeeId = entity.EmployeeId,
			BplCardNo = entity.BplCardNo,
			PersonWithDisability = entity.PersonWithDisability,
			SecondLanguage = entity.SecondLanguage,
			LibraryMember = entity.LibraryMember,
			TransportMember = entity.TransportMember,
			CreatedAt = entity.CreationTime,
			LastModifiedAt = entity.LastModificationTime ?? entity.CreationTime
		};
	}
}

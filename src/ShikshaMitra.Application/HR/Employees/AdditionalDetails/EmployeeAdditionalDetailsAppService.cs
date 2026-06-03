using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.HR.Employees.AdditionalDetails;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View)]
public class EmployeeAdditionalDetailsAppService(
	IEmployeeRepository employeeRepository,
	IEmployeeAdditionalDetailsRepository additionalDetailsRepository,
	IEmployeeAuthorizationService employeeAuthorizationService)
	: ApplicationService, IEmployeeAdditionalDetailsAppService
{
	/// <inheritdoc />
	public async Task<EmployeeAdditionalDetailsDto> GetAsync(Guid employeeId)
	{
		await employeeAuthorizationService.CheckEmployeeAccessAsync(employeeId);

		var employee = await employeeRepository.FindAsync(employeeId);
		if (employee == null)
		{
			throw new EntityNotFoundException(typeof(Employee), employeeId);
		}

		var additionalDetails = await additionalDetailsRepository.GetByEmployeeIdAsync(employeeId);
		if (additionalDetails == null)
		{
			return EmployeeAdditionalDetailsProjection.ToEmptyResponse(employee);
		}

		return ObjectMapper.Map<EmployeeAdditionalDetails, EmployeeAdditionalDetailsDto>(additionalDetails);
	}

	/// <inheritdoc />
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add)]
	public async Task<(EmployeeAdditionalDetailsDto Result, bool IsCreated)> UpsertAsync(
		Guid employeeId,
		EmployeeAdditionalDetailsUpsertDto input)
	{
		await employeeAuthorizationService.CheckEmployeeModifyAccessAsync(employeeId);

		var employee = await employeeRepository.FindAsync(employeeId);
		if (employee == null)
		{
			throw new EntityNotFoundException(typeof(Employee), employeeId);
		}

		var existingDetails = await additionalDetailsRepository.GetByEmployeeIdAsync(employeeId);

		EmployeeAdditionalDetails additionalDetails;
		bool isCreated;

		if (existingDetails == null)
		{
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

		return (ObjectMapper.Map<EmployeeAdditionalDetails, EmployeeAdditionalDetailsDto>(additionalDetails), isCreated);
	}

	/// <inheritdoc />
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add)]
	public async Task<EmployeeAdditionalDetailsDto> PatchAsync(
		Guid employeeId,
		EmployeeAdditionalDetailsPatchDto input)
	{
		await employeeAuthorizationService.CheckEmployeeModifyAccessAsync(employeeId);

		var employee = await employeeRepository.FindAsync(employeeId);
		if (employee == null)
		{
			throw new EntityNotFoundException(typeof(Employee), employeeId);
		}

		var additionalDetails = await additionalDetailsRepository.GetByEmployeeIdAsync(employeeId);
		if (additionalDetails == null)
		{
			throw new EntityNotFoundException(typeof(EmployeeAdditionalDetails), employeeId);
		}

		additionalDetails.Patch(
			bplCardNo: input.BplCardNo,
			personWithDisability: input.PersonWithDisability,
			secondLanguage: input.SecondLanguage,
			libraryMember: input.LibraryMember,
			transportMember: input.TransportMember,
			bplCardNoProvided: input.BplCardNoProvided);

		await additionalDetailsRepository.UpdateAsync(additionalDetails);

		return ObjectMapper.Map<EmployeeAdditionalDetails, EmployeeAdditionalDetailsDto>(additionalDetails);
	}
}

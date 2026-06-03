using ShikshaMitra.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.HR.Employees;

[RemoteService(false)]
public class EmployeeLegacyFacadeWorkflow(
	IEmployeeRepository employeeRepository,
	IEmployeeAdditionalDetailsRepository additionalDetailsRepository,
	EmployeeManager employeeManager,
	EmployeeAuthorizationService authorizationService,
	IEmployeeCacheManager employeeCacheManager,
	EmployeeLegacyListReader employeeLegacyListReader)
	: ApplicationService
{
	public async Task<EmployeeDto> UpdateFamilyInfo(Guid employeeId, UpdateEmployeeFamilyDetailsDto input)
	{
		await authorizationService.CheckEmployeeModifyAccessAsync(employeeId);

		var fatherDetails = MapContactDetails(input.FatherDetails);
		var motherDetails = MapContactDetails(input.MotherDetails);
		var spouseDetails = MapContactDetails(input.SpouseDetails);

		await employeeManager.UpdateFamilyInfoByEmployeeIdAsync(employeeId, fatherDetails, motherDetails, spouseDetails);

		var employee = await employeeRepository.GetAsync(employeeId);
		await employeeCacheManager.InvalidateEmployeeAsync(employeeId);

		return ObjectMapper.Map<Employee, EmployeeDto>(employee);
	}

	public async Task<EmployeeDto> UpdateExperienceInfo(Guid employeeId, List<CreateUpdateWorkExperienceDto> input)
	{
		var workExperiences =
			ObjectMapper.Map<List<CreateUpdateWorkExperienceDto>, List<WorkExperience>>(input);

		var employee = await employeeManager.UpdateExperienceInfoByEmployeeIdAsync(employeeId, workExperiences);
		employee = await employeeRepository.UpdateAsync(employee);

		return ObjectMapper.Map<Employee, EmployeeDto>(employee);
	}

	public async Task<EmployeeAdditionalDetailsDto> GetAdditionalDetailsAsync(Guid employeeId)
	{
		var employee = await employeeRepository.GetAsync(employeeId)
			?? throw new EntityNotFoundException(typeof(Employee), employeeId);

		var additionalDetails = await additionalDetailsRepository.GetByEmployeeIdAsync(employeeId);
		if (additionalDetails == null)
		{
			return new EmployeeAdditionalDetailsDto
			{
				EmployeeCode = employee.EmployeeCode,
				BplCardNo = null,
				PersonWithDisability = false,
				SecondLanguage = null,
				LibraryMember = false,
				TransportMember = false
			};
		}

		return new EmployeeAdditionalDetailsDto
		{
			EmployeeCode = employee.EmployeeCode,
			BplCardNo = additionalDetails.BplCardNo,
			PersonWithDisability = additionalDetails.PersonWithDisability,
			SecondLanguage = additionalDetails.SecondLanguage,
			LibraryMember = additionalDetails.LibraryMember,
			TransportMember = additionalDetails.TransportMember
		};
	}

	public async Task<EmployeeAdditionalDetailsDto> UpdateAdditionalDetailsAsync(
		Guid employeeId,
		UpdateEmployeeAdditionalDetailsDto input)
	{
		var employee = await employeeRepository.GetAsync(employeeId)
			?? throw new EntityNotFoundException(typeof(Employee), employeeId);

		var existingDetails = await additionalDetailsRepository.GetByEmployeeIdAsync(employeeId);

		EmployeeAdditionalDetails additionalDetails;
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
		}

		return new EmployeeAdditionalDetailsDto
		{
			EmployeeCode = employee.EmployeeCode,
			BplCardNo = additionalDetails.BplCardNo,
			PersonWithDisability = additionalDetails.PersonWithDisability,
			SecondLanguage = additionalDetails.SecondLanguage,
			LibraryMember = additionalDetails.LibraryMember,
			TransportMember = additionalDetails.TransportMember
		};
	}

	public async Task<EmployeeDto> UpdateEmployeeStatus(
		Guid employeeId,
		EmployeeStatusType employeeStatusType,
		string? changeReason,
		DateOnly? effectiveDate = null)
	{
		if (employeeStatusType != EmployeeStatusType.Active)
		{
			await EnsureNoActiveDirectReportsAsync(employeeId);
			effectiveDate ??= DateOnly.FromDateTime(Clock.Now.Date);
		}
		else
		{
			effectiveDate = null;
		}

		var employee = await employeeManager.UpdateEmployeeStatusAsync(employeeId, employeeStatusType, changeReason, effectiveDate);
		employee = await employeeRepository.UpdateAsync(employee);

		return ObjectMapper.Map<Employee, EmployeeDto>(employee);
	}

	public async Task<EmployeeListResponse> GetFilteredListAsync(GetEmployeeListInput input)
	{
		return await employeeLegacyListReader.GetFilteredListAsync(input);
	}

	private ContactDetails? MapContactDetails(ContactDetailsDto? input) =>
		input == null
			? null
			: ObjectMapper.Map<ContactDetailsDto, ContactDetails>(input);

	private async Task EnsureNoActiveDirectReportsAsync(Guid employeeId)
	{
		var employeeQuery = await employeeRepository.GetQueryableAsync();
		var activeDirectReportCount = await AsyncExecuter.CountAsync(
			employeeQuery.Where(e =>
				e.ReportingManagerId == employeeId &&
				e.EmployeeStatusType == EmployeeStatusType.Active));

		if (activeDirectReportCount > 0)
		{
			throw new BusinessException(HRErrorCodes.ReportingManagerHasActiveReports)
				.WithData("EmployeeId", employeeId)
				.WithData("ActiveDirectReportCount", activeDirectReportCount);
		}
	}

}

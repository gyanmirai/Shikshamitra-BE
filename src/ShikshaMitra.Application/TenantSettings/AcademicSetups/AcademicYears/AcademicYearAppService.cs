using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.TenantSettings.AcademicSetups;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.TenantSettings.AcademicYears;

[Authorize]
public class AcademicYearAppService(
	IAcademicYearRepository academicYearRepository,
	AcademicYearManager academicYearManager)
	:
		CrudAppService<AcademicYear, AcademicYearDto, Guid, PagedAndSortedResultRequestDto,
			CreateUpdateAcademicYearDto>(academicYearRepository),
		IAcademicYearsAppService
{
	public override async Task<AcademicYearDto> CreateAsync(CreateUpdateAcademicYearDto input)
	{
		AcademicYear academicYear = await academicYearManager.CreateAsync(
			input.Name,
			input.StartDate,
			input.EndDate
		);

		await academicYearRepository.InsertAsync(academicYear);
		return ObjectMapper.Map<AcademicYear, AcademicYearDto>(academicYear);
	}

	public override async Task<AcademicYearDto> UpdateAsync(Guid id, CreateUpdateAcademicYearDto input)
	{
		AcademicYear academicYear = await academicYearRepository.GetAsync(id);
		await academicYearManager.UpdateAsync(academicYear, input.Name, input.StartDate, input.EndDate);
		return ObjectMapper.Map<AcademicYear, AcademicYearDto>(academicYear);
	}
}
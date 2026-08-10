using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmAdmissionYearReferenceReader(
	IRepository<AcademicYear, Guid> academicYearRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<EducationMediumStage, Guid> mediumStageRepository,
	IRepository<EducationMedium, Guid> mediumRepository,
	IRepository<EducationBoard, Guid> boardRepository) : ApplicationService, ITransientDependency
{
	internal async Task<AcademicYear> GetAcademicYearAsync(Guid academicYearId)
	{
		var year = await academicYearRepository.FindAsync(academicYearId);
		if (year is null || year.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException("AdmissionCrm:AcademicYearNotFound")
				.WithData("AcademicYearId", academicYearId);
		}

		return year;
	}

	internal async Task<List<AdmissionCrmAdmissionYearStandardRow>> GetStandardRowsAsync(
		Guid academicYearId)
	{
		var standards = await standardRepository.GetQueryableAsync();
		var stages = await mediumStageRepository.GetQueryableAsync();
		var mediums = await mediumRepository.GetQueryableAsync();
		var boards = await boardRepository.GetQueryableAsync();
		var query =
			from standard in standards
			join stage in stages on standard.EducationMediumStageId equals stage.Id
			join medium in mediums on stage.EducationMediumId equals medium.Id
			join board in boards on medium.EducationBoardId equals board.Id
			where board.AcademicYearId == academicYearId
			select new AdmissionCrmAdmissionYearStandardRow(
				standard.Id,
				standard.Name,
				stage.Id,
				stage.Name,
				medium.Id,
				medium.Name,
				board.Id,
				board.Type);
		return await AsyncExecuter.ToListAsync(query);
	}
}

internal sealed record AdmissionCrmAdmissionYearStandardRow(Guid StandardId, string StandardName,
	Guid MediumStageId, string MediumStageName, Guid MediumId, string MediumName,
	Guid BoardId, EducationBoardType BoardType);
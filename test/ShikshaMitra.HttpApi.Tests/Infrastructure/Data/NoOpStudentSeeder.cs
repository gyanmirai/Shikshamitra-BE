using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Seeders;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.EducationBoards;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Infrastructure.Data;

/// <summary>
/// Replaces the real <see cref="StudentSeederContributor"/> in HTTP API tests
/// to avoid a deadlock caused by lazy-loaded navigation properties during
/// student creation on SQLite.
/// </summary>
internal sealed class NoOpStudentSeeder : StudentSeederContributor
{
	public NoOpStudentSeeder(
		ICurrentTenant currentTenant,
		IGuidGenerator guidGenerator,
		IUnitOfWork unitOfWork,
		IRepository<Student, Guid> studentRepository,
		IAcademicYearRepository academicYearRepository,
		IRepository<EducationBoard, Guid> educationBoardRepository,
		IEducationMediumRepository educationMediumRepository,
		IEducationMediumStageRepository educationMediumStageRepository,
		IStandardRepository standardRepository,
		IDivisionRepository divisionRepository,
		ILogger<StudentSeederContributor> logger)
		: base(currentTenant, guidGenerator, unitOfWork,
			studentRepository, academicYearRepository,
			educationBoardRepository, educationMediumRepository, educationMediumStageRepository,
			standardRepository, divisionRepository, logger)
	{
	}

	public override Task SeedAsync(DataSeedContext context) => Task.CompletedTask;
}

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.TenantSettings;

/// <summary>
/// Seeds required academic setup data (AcademicYear, EducationBoard, EducationMedium)
/// This ensures foreign key constraints are satisfied for Student entities
/// </summary>
public class AcademicSetupDataSeeder : IDataSeedContributor, ITransientDependency
{
    private readonly IAcademicYearRepository _academicYearRepository;
    private readonly IEducationBoardRepository _educationBoardRepository;
    private readonly IEducationMediumRepository _educationMediumRepository;
    private readonly AcademicYearManager _academicYearManager;
    private readonly EducationBoardManager _educationBoardManager;
    private readonly EducationMediumManager _educationMediumManager;
    private readonly ILogger<AcademicSetupDataSeeder> _logger;

    public AcademicSetupDataSeeder(
        IAcademicYearRepository academicYearRepository,
        IEducationBoardRepository educationBoardRepository,
        IEducationMediumRepository educationMediumRepository,
        AcademicYearManager academicYearManager,
        EducationBoardManager educationBoardManager,
        EducationMediumManager educationMediumManager,
        ILogger<AcademicSetupDataSeeder> logger)
    {
        _academicYearRepository = academicYearRepository;
        _educationBoardRepository = educationBoardRepository;
        _educationMediumRepository = educationMediumRepository;
        _academicYearManager = academicYearManager;
        _educationBoardManager = educationBoardManager;
        _educationMediumManager = educationMediumManager;
        _logger = logger;
    }

    [UnitOfWork]
    public async Task SeedAsync(DataSeedContext context)
    {
        _logger.LogInformation(
            "Skipping minimal academic setup seeder. Full academic setup is handled by AcademicSetupSeederContributor.");
        await Task.CompletedTask;
    }
}

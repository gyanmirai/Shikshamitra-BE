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
        _logger.LogInformation("Starting Academic Setup data seeding...");

        // Check if already seeded
        if (await _educationMediumRepository.GetCountAsync() > 0)
        {
            _logger.LogInformation("Academic setup data already exists. Skipping seed.");
            return;
        }

        try
        {
            // 1. Create Academic Year (skip if already exists)
            var currentYear = DateTime.Now.Year;
            var nextYear = currentYear + 1;
            var yearName = $"{currentYear}-{nextYear}";
            var existingYear = await _academicYearRepository.FindAsync(x => x.Name == yearName);
            ShikshaMitra.TenantSettings.AcademicYears.AcademicYear academicYear;
            if (existingYear != null)
            {
                academicYear = existingYear;
                _logger.LogInformation("Academic Year already exists: {Name} (ID: {Id})", academicYear.Name, academicYear.Id);
            }
            else
            {
                academicYear = await _academicYearManager.CreateAsync(
                    name: yearName,
                    startDate: new DateTime(currentYear, 6, 1),
                    endDate: new DateTime(nextYear, 5, 31)
                );
                await _academicYearRepository.InsertAsync(academicYear, autoSave: true);
                _logger.LogInformation("Created Academic Year: {Name} (ID: {Id})", academicYear.Name, academicYear.Id);
            }

            // 2. Create Education Boards (skip if already exists)
            var educationBoards = new[]
            {
                EducationBoardType.CBSE,
                EducationBoardType.GSEB
            };

            foreach (var boardType in educationBoards)
            {
                var existingBoard = await _educationBoardRepository.FindAsync(
                    x => x.Type == boardType && x.AcademicYearId == academicYear.Id
                );

                EducationBoard board;
                if (existingBoard != null)
                {
                    board = existingBoard;
                    _logger.LogInformation("Education Board already exists: {Type} (ID: {Id})", boardType, board.Id);
                }
                else
                {
                    board = await _educationBoardManager.CreateAsync(
                        academicYearId: academicYear.Id,
                        educationBoardType: boardType
                    );
                    await _educationBoardRepository.InsertAsync(board, autoSave: true);
                    _logger.LogInformation("Created Education Board: {Type} (ID: {Id})", boardType, board.Id);
                }

                // 3. Create Education Mediums for each board (skip if already exists)
                var mediums = new[]
                {
                    "English Medium",
                    "Hindi Medium",
                    "Regional Medium"
                };

                foreach (var mediumName in mediums)
                {
                    var existingMedium = await _educationMediumRepository.FindAsync(
                        x => x.EducationBoardId == board.Id && x.Name == mediumName
                    );
                    if (existingMedium != null)
                    {
                        _logger.LogInformation("Education Medium already exists: {Name} for Board: {BoardType}", mediumName, boardType);
                        continue;
                    }

                    var medium = await _educationMediumManager.CreateAsync(
                        educationBoardId: board.Id,
                        name: mediumName
                    );
                    await _educationMediumRepository.InsertAsync(medium, autoSave: true);
                    _logger.LogInformation("Created Education Medium: {Name} for Board: {BoardType} (ID: {Id})",
                        mediumName, boardType, medium.Id);
                }
            }

            _logger.LogInformation("Academic Setup data seeding completed successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while seeding academic setup data");
            throw;
        }
    }
}

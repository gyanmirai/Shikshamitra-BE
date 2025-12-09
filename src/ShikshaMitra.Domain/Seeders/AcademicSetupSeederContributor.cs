using ShikshaMitra.Onboarding.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

/// <summary>
/// Seeds academic setup data for multiple years including Academic Years, Education Boards, Mediums, Stages, Standards, and Divisions.
/// Creates comprehensive test data for Last Year, Current Year, and Upcoming Year.
/// </summary>
public class AcademicSetupSeederContributor : BaseSeederContributor
{
	private readonly IAcademicYearRepository _academicYearRepository;
	private readonly AcademicYearManager _academicYearManager;
	private readonly IEducationBoardRepository _educationBoardRepository;
	private readonly EducationBoardManager _educationBoardManager;
	private readonly IEducationMediumRepository _educationMediumRepository;
	private readonly EducationMediumManager _educationMediumManager;
	private readonly IEducationMediumStageRepository _educationMediumStageRepository;
	private readonly EducationMediumStageManager _educationMediumStageManager;
	private readonly IStandardRepository _standardRepository;
	private readonly StandardManager _standardManager;
	private readonly IDivisionRepository _divisionRepository;
	private readonly DivisionManager _divisionManager;

	public AcademicSetupSeederContributor(
		ICurrentTenant currentTenant,
		IGuidGenerator guidGenerator,
		IUnitOfWork unitOfWork,
		IAcademicYearRepository academicYearRepository,
		AcademicYearManager academicYearManager,
		IEducationBoardRepository educationBoardRepository,
		EducationBoardManager educationBoardManager,
		IEducationMediumRepository educationMediumRepository,
		EducationMediumManager educationMediumManager,
		IEducationMediumStageRepository educationMediumStageRepository,
		EducationMediumStageManager educationMediumStageManager,
		IStandardRepository standardRepository,
		StandardManager standardManager,
		IDivisionRepository divisionRepository,
		DivisionManager divisionManager)
		: base(currentTenant, guidGenerator, unitOfWork)
	{
		_academicYearRepository = academicYearRepository;
		_academicYearManager = academicYearManager;
		_educationBoardRepository = educationBoardRepository;
		_educationBoardManager = educationBoardManager;
		_educationMediumRepository = educationMediumRepository;
		_educationMediumManager = educationMediumManager;
		_educationMediumStageRepository = educationMediumStageRepository;
		_educationMediumStageManager = educationMediumStageManager;
		_standardRepository = standardRepository;
		_standardManager = standardManager;
		_divisionRepository = divisionRepository;
		_divisionManager = divisionManager;
	}

	public override async Task SeedAsync(DataSeedContext context)
	{
		// Check if already seeded
		if (await _academicYearRepository.GetCountAsync() > 0)
			return;

		// Seed academic data for 3 years: Last year, Current year, Upcoming year
		var currentYear = DateTime.Now.Year;
		
		// Last Year (e.g., 2023-2024)
		await SeedAcademicYearDataAsync(currentYear - 1);
		
		// Current Year (e.g., 2024-2025)
		await SeedAcademicYearDataAsync(currentYear);
		
		// Upcoming Year (e.g., 2025-2026)
		await SeedAcademicYearDataAsync(currentYear + 1);
	}

	private async Task SeedAcademicYearDataAsync(int startYear)
	{
		// 1. Seed Academic Year
		var academicYear = await SeedAcademicYearAsync(startYear);

		// 2. Seed Education Boards (CBSE and GSEB)
		var boards = await SeedEducationBoardsAsync(academicYear.Id);

		// 3. Seed Education Mediums for each board
		var mediumsMap = await SeedEducationMediumsAsync(boards);

		// 4. Seed Education Medium Stages for each medium
		var stagesMap = await SeedEducationMediumStagesAsync(mediumsMap);

		// 5. Seed Standards for each stage
		var standardsMap = await SeedStandardsAsync(stagesMap);

		// 6. Seed Divisions for each standard
		await SeedDivisionsAsync(standardsMap);
	}

	private async Task<AcademicYear> SeedAcademicYearAsync(int startYear)
	{
		var endYear = startYear + 1;
		var academicYearName = $"{startYear}-{endYear}";

		var academicYear = await _academicYearManager.CreateAsync(
			name: academicYearName,
			startDate: new DateTime(startYear, 6, 1),
			endDate: new DateTime(endYear, 5, 31)
		);

		await _academicYearRepository.InsertAsync(academicYear);
		await CurrentUnitOfWork.SaveChangesAsync();

		return academicYear;
	}

	private async Task<List<EducationBoard>> SeedEducationBoardsAsync(Guid academicYearId)
	{
		var boards = new List<EducationBoard>();

		// CBSE Board
		var cbseBoard = await _educationBoardManager.CreateAsync(
			academicYearId: academicYearId,
			educationBoardType: EducationBoardType.CBSE
		);
		boards.Add(cbseBoard);
		await _educationBoardRepository.InsertAsync(cbseBoard);

		// GSEB Board
		var gsebBoard = await _educationBoardManager.CreateAsync(
			academicYearId: academicYearId,
			educationBoardType: EducationBoardType.GSEB
		);
		boards.Add(gsebBoard);
		await _educationBoardRepository.InsertAsync(gsebBoard);

		await CurrentUnitOfWork.SaveChangesAsync();
		return boards;
	}

	private async Task<Dictionary<Guid, List<EducationMedium>>> SeedEducationMediumsAsync(List<EducationBoard> boards)
	{
		var mediumsMap = new Dictionary<Guid, List<EducationMedium>>();

		foreach (var board in boards)
		{
			var mediums = new List<EducationMedium>();

			// English Medium (Common for both boards)
			var englishMedium = await _educationMediumManager.CreateAsync(
				educationBoardId: board.Id,
				name: "English"
			);
			mediums.Add(englishMedium);
			await _educationMediumRepository.InsertAsync(englishMedium);

			// Hindi Medium (Common for both boards)
			var hindiMedium = await _educationMediumManager.CreateAsync(
				educationBoardId: board.Id,
				name: "Hindi"
			);
			mediums.Add(hindiMedium);
			await _educationMediumRepository.InsertAsync(hindiMedium);

			// Board-specific mediums
			if (board.Type == EducationBoardType.GSEB)
			{
				// Gujarati Medium (for GSEB)
				var gujaratiMedium = await _educationMediumManager.CreateAsync(
					educationBoardId: board.Id,
					name: "Gujarati"
				);
				mediums.Add(gujaratiMedium);
				await _educationMediumRepository.InsertAsync(gujaratiMedium);

				// Semi-English Medium (for GSEB)
				var semiEnglishMedium = await _educationMediumManager.CreateAsync(
					educationBoardId: board.Id,
					name: "Semi-English"
				);
				mediums.Add(semiEnglishMedium);
				await _educationMediumRepository.InsertAsync(semiEnglishMedium);
			}
			else if (board.Type == EducationBoardType.CBSE)
			{
				// Sanskrit Medium (for CBSE)
				var sanskritMedium = await _educationMediumManager.CreateAsync(
					educationBoardId: board.Id,
					name: "Sanskrit"
				);
				mediums.Add(sanskritMedium);
				await _educationMediumRepository.InsertAsync(sanskritMedium);
			}

			mediumsMap[board.Id] = mediums;
		}

		await CurrentUnitOfWork.SaveChangesAsync();
		return mediumsMap;
	}

	private async Task<Dictionary<Guid, List<EducationMediumStage>>> SeedEducationMediumStagesAsync(
		Dictionary<Guid, List<EducationMedium>> mediumsMap)
	{
		var stagesMap = new Dictionary<Guid, List<EducationMediumStage>>();

		foreach (var mediums in mediumsMap.Values)
		{
			foreach (var medium in mediums)
			{
				var stages = new List<EducationMediumStage>();

				// Pre-Primary Stage (Nursery, LKG, UKG)
				var prePrimaryStage = await _educationMediumStageManager.CreateAsync(
					educationMediumId: medium.Id,
					name: "Pre-Primary"
				);
				stages.Add(prePrimaryStage);
				await _educationMediumStageRepository.InsertAsync(prePrimaryStage);

				// Primary Stage (Classes 1-5)
				var primaryStage = await _educationMediumStageManager.CreateAsync(
					educationMediumId: medium.Id,
					name: "Primary"
				);
				stages.Add(primaryStage);
				await _educationMediumStageRepository.InsertAsync(primaryStage);

				// Secondary Stage (Classes 6-10)
				var secondaryStage = await _educationMediumStageManager.CreateAsync(
					educationMediumId: medium.Id,
					name: "Secondary"
				);
				stages.Add(secondaryStage);
				await _educationMediumStageRepository.InsertAsync(secondaryStage);

				// Higher Secondary Stage (Classes 11-12)
				var higherSecondaryStage = await _educationMediumStageManager.CreateAsync(
					educationMediumId: medium.Id,
					name: "Higher Secondary"
				);
				stages.Add(higherSecondaryStage);
				await _educationMediumStageRepository.InsertAsync(higherSecondaryStage);

				stagesMap[medium.Id] = stages;
			}
		}

		await CurrentUnitOfWork.SaveChangesAsync();
		return stagesMap;
	}

	private async Task<Dictionary<Guid, List<Standard>>> SeedStandardsAsync(
		Dictionary<Guid, List<EducationMediumStage>> stagesMap)
	{
		var standardsMap = new Dictionary<Guid, List<Standard>>();

		foreach (var stages in stagesMap.Values)
		{
			foreach (var stage in stages)
			{
				var standards = new List<Standard>();

				if (stage.Name == "Pre-Primary")
				{
					// Nursery, LKG, UKG
					var prePrimaryClasses = new[] { ("Nursery", 3), ("LKG", 4), ("UKG", 5) };
					foreach (var (className, ageLimit) in prePrimaryClasses)
					{
						var standard = await _standardManager.CreateAsync(
							educationMediumStageId: stage.Id,
							name: className,
							ageLowerLimit: ageLimit,
							shiftId: null
						);
						standards.Add(standard);
						await _standardRepository.InsertAsync(standard);
					}
				}
				else if (stage.Name == "Primary")
				{
					// Classes 1-5
					for (int i = 1; i <= 5; i++)
					{
						var standard = await _standardManager.CreateAsync(
							educationMediumStageId: stage.Id,
							name: $"Class {i}",
							ageLowerLimit: 5 + i,
							shiftId: null
						);
						standards.Add(standard);
						await _standardRepository.InsertAsync(standard);
					}
				}
				else if (stage.Name == "Secondary")
				{
					// Classes 6-10
					for (int i = 6; i <= 10; i++)
					{
						var standard = await _standardManager.CreateAsync(
							educationMediumStageId: stage.Id,
							name: $"Class {i}",
							ageLowerLimit: 5 + i,
							shiftId: null
						);
						standards.Add(standard);
						await _standardRepository.InsertAsync(standard);
					}
				}
				else if (stage.Name == "Higher Secondary")
				{
					// Classes 11-12
					for (int i = 11; i <= 12; i++)
					{
						var standard = await _standardManager.CreateAsync(
							educationMediumStageId: stage.Id,
							name: $"Class {i}",
							ageLowerLimit: 5 + i,
							shiftId: null
						);
						standards.Add(standard);
						await _standardRepository.InsertAsync(standard);
					}
				}

				standardsMap[stage.Id] = standards;
			}
		}

		await CurrentUnitOfWork.SaveChangesAsync();
		return standardsMap;
	}

	private async Task SeedDivisionsAsync(Dictionary<Guid, List<Standard>> standardsMap)
	{
		foreach (var standards in standardsMap.Values)
		{
			foreach (var standard in standards)
			{
				// Determine divisions based on class level for realistic data
				string[] divisionNames;
				int strength;

				if (standard.Name == "Nursery" || standard.Name == "LKG" || standard.Name == "UKG")
				{
					// Pre-Primary: 2 divisions with smaller strength
					divisionNames = new[] { "A", "B" };
					strength = 25;
				}
				else if (standard.Name.StartsWith("Class") && 
				         (standard.Name == "Class 1" || standard.Name == "Class 2"))
				{
					// Lower Primary: 3 divisions
					divisionNames = new[] { "A", "B", "C" };
					strength = 35;
				}
				else if (standard.Name.StartsWith("Class") && 
				         int.TryParse(standard.Name.Replace("Class ", ""), out int classNum) && 
				         classNum >= 3 && classNum <= 8)
				{
					// Middle Primary & Secondary: 4 divisions
					divisionNames = new[] { "A", "B", "C", "D" };
					strength = 40;
				}
				else if (standard.Name.StartsWith("Class") && 
				         int.TryParse(standard.Name.Replace("Class ", ""), out int classNum2) && 
				         classNum2 >= 9 && classNum2 <= 10)
				{
					// Classes 9-10: 5 divisions (high enrollment)
					divisionNames = new[] { "A", "B", "C", "D", "E" };
					strength = 45;
				}
				else
				{
					// Classes 11-12: 3 divisions (stream-based: Science, Commerce, Arts)
					divisionNames = new[] { "A", "B", "C" };
					strength = 35;
				}

				foreach (var divisionName in divisionNames)
				{
					var division = await _divisionManager.CreateAsync(
						standardId: standard.Id,
						name: divisionName,
						strength: strength
					);
					await _divisionRepository.InsertAsync(division);
				}
			}
		}

		await CurrentUnitOfWork.SaveChangesAsync();
	}
}

using Microsoft.Extensions.Logging;
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
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

/// <summary>
/// Seeds the full demo academic graph using prevalidated batch inserts.
/// </summary>
public class AcademicSetupSeederContributor : BaseSeederContributor
{
	private readonly IAcademicYearRepository _academicYearRepository;
	private readonly IEducationBoardRepository _educationBoardRepository;
	private readonly IEducationMediumRepository _educationMediumRepository;
	private readonly IEducationMediumStageRepository _educationMediumStageRepository;
	private readonly IStandardRepository _standardRepository;
	private readonly IDivisionRepository _divisionRepository;
	private readonly ILogger<AcademicSetupSeederContributor> _logger;

	public AcademicSetupSeederContributor(
		ICurrentTenant currentTenant,
		IGuidGenerator guidGenerator,
		IUnitOfWork unitOfWork,
		IAcademicYearRepository academicYearRepository,
		IEducationBoardRepository educationBoardRepository,
		IEducationMediumRepository educationMediumRepository,
		IEducationMediumStageRepository educationMediumStageRepository,
		IStandardRepository standardRepository,
		IDivisionRepository divisionRepository,
		ILogger<AcademicSetupSeederContributor> logger)
		: base(currentTenant, guidGenerator, unitOfWork)
	{
		_academicYearRepository = academicYearRepository;
		_educationBoardRepository = educationBoardRepository;
		_educationMediumRepository = educationMediumRepository;
		_educationMediumStageRepository = educationMediumStageRepository;
		_standardRepository = standardRepository;
		_divisionRepository = divisionRepository;
		_logger = logger;
	}

	public override async Task SeedAsync(DataSeedContext context)
	{
		var expectedYears = TimePhase(
			"graph build",
			() => BuildExpectedAcademicYears(DateTime.Now.Year));
		ValidateExpectedGraph(expectedYears);

		var existing = await TimePhaseAsync("preload", PreloadExistingAcademicSetupAsync);

		var yearsByName = existing.Years.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
		var boardsByKey = existing.Boards.ToDictionary(x => (x.AcademicYearId, x.Type));
		var mediumsByKey = existing.Mediums.ToDictionary(x => (x.EducationBoardId, Name: x.Name), GuidStringKeyComparer.Instance);
		var stagesByKey = existing.Stages.ToDictionary(x => (x.EducationMediumId, Name: x.Name), GuidStringKeyComparer.Instance);
		var standardsByKey = existing.Standards.ToDictionary(x => (x.EducationMediumStageId, Name: x.Name), GuidStringKeyComparer.Instance);
		var divisionsByKey = existing.Divisions.ToDictionary(x => (x.StandardId, Name: x.Name), GuidStringKeyComparer.Instance);

		var yearsToInsert = new List<AcademicYear>();
		var boardsToInsert = new List<EducationBoard>();
		var mediumsToInsert = new List<EducationMedium>();
		var stagesToInsert = new List<EducationMediumStage>();
		var standardsToInsert = new List<Standard>();
		var divisionsToInsert = new List<Division>();

		foreach (var yearSpec in expectedYears)
		{
			if (!yearsByName.TryGetValue(yearSpec.Name, out var academicYear))
			{
				academicYear = new AcademicYear(
					GuidGenerator.Create(),
					CurrentTenant.Id,
					yearSpec.Name,
					yearSpec.StartDate,
					yearSpec.EndDate);
				yearsByName[yearSpec.Name] = academicYear;
				yearsToInsert.Add(academicYear);
			}

			foreach (var boardSpec in yearSpec.Boards)
			{
				if (!boardsByKey.TryGetValue((academicYear.Id, boardSpec.Type), out var board))
				{
					board = new EducationBoard(
						GuidGenerator.Create(),
						CurrentTenant.Id,
						boardSpec.Type,
						academicYear.Id);
					boardsByKey[(academicYear.Id, boardSpec.Type)] = board;
					boardsToInsert.Add(board);
				}

				foreach (var mediumSpec in boardSpec.Mediums)
				{
					if (!mediumsByKey.TryGetValue((board.Id, mediumSpec.Name), out var medium))
					{
						medium = new EducationMedium(
							GuidGenerator.Create(),
							CurrentTenant.Id,
							mediumSpec.Name,
							board.Id);
						mediumsByKey[(board.Id, mediumSpec.Name)] = medium;
						mediumsToInsert.Add(medium);
					}

					foreach (var stageSpec in mediumSpec.Stages)
					{
						if (!stagesByKey.TryGetValue((medium.Id, stageSpec.Name), out var stage))
						{
							stage = new EducationMediumStage(
								GuidGenerator.Create(),
								CurrentTenant.Id,
								stageSpec.Name,
								bannerPicture: null,
								medium.Id);
							stagesByKey[(medium.Id, stageSpec.Name)] = stage;
							stagesToInsert.Add(stage);
						}

						foreach (var standardSpec in stageSpec.Standards)
						{
							if (!standardsByKey.TryGetValue((stage.Id, standardSpec.Name), out var standard))
							{
								standard = new Standard(
									GuidGenerator.Create(),
									CurrentTenant.Id,
									standardSpec.Name,
									standardSpec.AgeLowerLimit,
									stage.Id,
									shiftId: null);
								standardsByKey[(stage.Id, standardSpec.Name)] = standard;
								standardsToInsert.Add(standard);
							}

							foreach (var divisionSpec in standardSpec.Divisions)
							{
								if (divisionsByKey.ContainsKey((standard.Id, divisionSpec.Name)))
								{
									continue;
								}

								var division = new Division(
									GuidGenerator.Create(),
									CurrentTenant.Id,
									divisionSpec.Name,
									standard.Id,
									divisionSpec.Strength,
									roomNumber: "B101");
								divisionsByKey[(standard.Id, divisionSpec.Name)] = division;
								divisionsToInsert.Add(division);
							}
						}
					}
				}
			}
		}

		await TimePhaseAsync(
			"inserts",
			async () =>
			{
				if (yearsToInsert.Count > 0)
				{
					await _academicYearRepository.InsertManyAsync(yearsToInsert, autoSave: false);
				}

				if (boardsToInsert.Count > 0)
				{
					await _educationBoardRepository.InsertManyAsync(boardsToInsert, autoSave: false);
				}

				if (mediumsToInsert.Count > 0)
				{
					await _educationMediumRepository.InsertManyAsync(mediumsToInsert, autoSave: false);
				}

				if (stagesToInsert.Count > 0)
				{
					await _educationMediumStageRepository.InsertManyAsync(stagesToInsert, autoSave: false);
				}

				if (standardsToInsert.Count > 0)
				{
					await _standardRepository.InsertManyAsync(standardsToInsert, autoSave: false);
				}

				if (divisionsToInsert.Count > 0)
				{
					await _divisionRepository.InsertManyAsync(divisionsToInsert, autoSave: false);
				}

				if (yearsToInsert.Count + boardsToInsert.Count + mediumsToInsert.Count + stagesToInsert.Count +
				    standardsToInsert.Count + divisionsToInsert.Count > 0)
				{
					await CurrentUnitOfWork.SaveChangesAsync();
				}
			});

		_logger.LogInformation(
			"[AcademicSetupSeeder] Ensured full academic setup. Inserted Years={Years}, Boards={Boards}, Mediums={Mediums}, Stages={Stages}, Standards={Standards}, Divisions={Divisions}",
			yearsToInsert.Count,
			boardsToInsert.Count,
			mediumsToInsert.Count,
			stagesToInsert.Count,
			standardsToInsert.Count,
			divisionsToInsert.Count);
	}

	private async Task<ExistingAcademicSetup> PreloadExistingAcademicSetupAsync()
	{
		var years = await _academicYearRepository.GetListAsync();
		var boards = await _educationBoardRepository.GetListAsync();
		var mediums = await _educationMediumRepository.GetListAsync();
		var stages = await _educationMediumStageRepository.GetListAsync();
		var standards = await _standardRepository.GetListAsync();
		var divisions = await _divisionRepository.GetListAsync();

		return new ExistingAcademicSetup(years, boards, mediums, stages, standards, divisions);
	}

	private static IReadOnlyList<AcademicYearSpec> BuildExpectedAcademicYears(int currentYear)
	{
		return Enumerable.Range(currentYear - 1, 3)
			.Select(startYear =>
			{
				var endYear = startYear + 1;
				return new AcademicYearSpec(
					$"{startYear}-{endYear}",
					new DateTime(startYear, 6, 1),
					new DateTime(endYear, 5, 31),
					[
						new BoardSpec(EducationBoardType.CBSE, [BuildMedium("English")]),
						new BoardSpec(EducationBoardType.GSEB, [BuildMedium("English"), BuildMedium("Gujarati")])
					]);
			})
			.ToList();
	}

	private static MediumSpec BuildMedium(string name) =>
		new(
			name,
			[
				new StageSpec("Pre-Primary", [new StandardSpec("Nursery", 3), new StandardSpec("LKG", 4), new StandardSpec("UKG", 5)]),
				new StageSpec("Primary", Enumerable.Range(1, 5).Select(x => new StandardSpec($"Class {x}", 5 + x)).ToList()),
				new StageSpec("Secondary", Enumerable.Range(6, 5).Select(x => new StandardSpec($"Class {x}", 5 + x)).ToList()),
				new StageSpec("Higher Secondary", Enumerable.Range(11, 2).Select(x => new StandardSpec($"Class {x}", 5 + x)).ToList())
			]);

	private static void ValidateExpectedGraph(IReadOnlyList<AcademicYearSpec> years)
	{
		ThrowIfDuplicate(years.Select(x => x.Name), "academic years");
		foreach (var year in years)
		{
			ThrowIfDuplicate(year.Boards.Select(x => x.Type.ToString()), $"boards for {year.Name}");
			foreach (var board in year.Boards)
			{
				ThrowIfDuplicate(board.Mediums.Select(x => x.Name), $"mediums for {year.Name}/{board.Type}");
				foreach (var medium in board.Mediums)
				{
					ThrowIfDuplicate(medium.Stages.Select(x => x.Name), $"stages for {year.Name}/{board.Type}/{medium.Name}");
					foreach (var stage in medium.Stages)
					{
						ThrowIfDuplicate(stage.Standards.Select(x => x.Name), $"standards for {stage.Name}");
						foreach (var standard in stage.Standards)
						{
							if (standard.AgeLowerLimit <= 0)
							{
								throw new InvalidOperationException($"Seed standard '{standard.Name}' has invalid age lower limit.");
							}

							ThrowIfDuplicate(standard.Divisions.Select(x => x.Name), $"divisions for {standard.Name}");
							if (standard.Divisions.Any(x => x.Strength <= 0))
							{
								throw new InvalidOperationException($"Seed standard '{standard.Name}' has invalid division strength.");
							}
						}
					}
				}
			}
		}
	}

	private static void ThrowIfDuplicate(IEnumerable<string> values, string scope)
	{
		var duplicates = values
			.GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
			.Where(x => x.Count() > 1)
			.Select(x => x.Key)
			.ToList();
		if (duplicates.Count > 0)
		{
			throw new InvalidOperationException($"Duplicate seed {scope}: {string.Join(", ", duplicates)}");
		}
	}

	private T TimePhase<T>(string phaseName, Func<T> action)
	{
		var stopwatch = Stopwatch.StartNew();
		var result = action();
		stopwatch.Stop();
		_logger.LogInformation("[AcademicSetupSeeder] {PhaseName} completed in {ElapsedMs} ms", phaseName, stopwatch.ElapsedMilliseconds);
		return result;
	}

	private async Task<T> TimePhaseAsync<T>(string phaseName, Func<Task<T>> action)
	{
		var stopwatch = Stopwatch.StartNew();
		var result = await action();
		stopwatch.Stop();
		_logger.LogInformation("[AcademicSetupSeeder] {PhaseName} completed in {ElapsedMs} ms", phaseName, stopwatch.ElapsedMilliseconds);
		return result;
	}

	private async Task TimePhaseAsync(string phaseName, Func<Task> action)
	{
		var stopwatch = Stopwatch.StartNew();
		await action();
		stopwatch.Stop();
		_logger.LogInformation("[AcademicSetupSeeder] {PhaseName} completed in {ElapsedMs} ms", phaseName, stopwatch.ElapsedMilliseconds);
	}

	private static string[] ResolveDivisionNames(string standardName)
	{
		var divisionCount = standardName switch
		{
			"Nursery" => 1,
			"LKG" => 2,
			"UKG" => 3,
			string name when name.StartsWith("Class ", StringComparison.OrdinalIgnoreCase) &&
			                 int.TryParse(name["Class ".Length..], out var classNumber) => ((classNumber - 1) % 3) + 1,
			_ => 1
		};

		return new[] { "A", "B", "C" }[..divisionCount];
	}

	private static int ResolveDivisionStrength(string standardName)
	{
		if (standardName is "Nursery" or "LKG" or "UKG")
		{
			return 25;
		}

		if (standardName is "Class 1" or "Class 2")
		{
			return 35;
		}

		if (standardName.StartsWith("Class ", StringComparison.OrdinalIgnoreCase) &&
		    int.TryParse(standardName["Class ".Length..], out var classNumber))
		{
			return classNumber switch
			{
				>= 3 and <= 8 => 40,
				>= 9 and <= 10 => 45,
				_ => 35
			};
		}

		return 35;
	}

	private sealed record ExistingAcademicSetup(
		List<AcademicYear> Years,
		List<EducationBoard> Boards,
		List<EducationMedium> Mediums,
		List<EducationMediumStage> Stages,
		List<Standard> Standards,
		List<Division> Divisions);

	private sealed record AcademicYearSpec(string Name, DateTime StartDate, DateTime EndDate, IReadOnlyList<BoardSpec> Boards);
	private sealed record BoardSpec(EducationBoardType Type, IReadOnlyList<MediumSpec> Mediums);
	private sealed record MediumSpec(string Name, IReadOnlyList<StageSpec> Stages);
	private sealed record StageSpec(string Name, IReadOnlyList<StandardSpec> Standards);

	private sealed record StandardSpec(string Name, int AgeLowerLimit)
	{
		public IReadOnlyList<DivisionSpec> Divisions { get; } = ResolveDivisionNames(Name)
			.Select(divisionName => new DivisionSpec(divisionName, ResolveDivisionStrength(Name)))
			.ToList();
	}

	private sealed record DivisionSpec(string Name, int Strength);

	private sealed class GuidStringKeyComparer : IEqualityComparer<(Guid, string)>
	{
		public static readonly GuidStringKeyComparer Instance = new();

		public bool Equals((Guid, string) x, (Guid, string) y) =>
			x.Item1 == y.Item1 &&
			StringComparer.OrdinalIgnoreCase.Equals(x.Item2, y.Item2);

		public int GetHashCode((Guid, string) obj) =>
			HashCode.Combine(
				obj.Item1,
				StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item2));
	}
}

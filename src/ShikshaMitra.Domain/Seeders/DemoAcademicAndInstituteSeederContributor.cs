using Microsoft.Extensions.Logging;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.Academic.Homework;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.RoomsManagement.Buildings;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

/// <summary>
/// Seeds institute rooms and academic reference data used by manual end-to-end testing.
/// </summary>
public class DemoAcademicAndInstituteSeederContributor : BaseSeederContributor
{
	private readonly IRepository<Building, Guid> _buildingRepository;
	private readonly IRepository<Floor, Guid> _floorRepository;
	private readonly IRepository<RoomType, Guid> _roomTypeRepository;
	private readonly IRepository<Room, Guid> _roomRepository;
	private readonly IEducationMediumRepository _educationMediumRepository;
	private readonly IRepository<Subject, Guid> _subjectRepository;
	private readonly IRepository<ExamType, Guid> _examTypeRepository;
	private readonly IHomeworkTypeRepository _homeworkTypeRepository;
	private readonly IAssignmentTypeRepository _assignmentTypeRepository;
	private readonly IRepository<HomeworkAssignmentType, Guid> _homeworkAssignmentTypeRepository;
	private readonly ILogger<DemoAcademicAndInstituteSeederContributor> _logger;

	public DemoAcademicAndInstituteSeederContributor(
		ICurrentTenant currentTenant,
		IGuidGenerator guidGenerator,
		IUnitOfWork unitOfWork,
		IRepository<Building, Guid> buildingRepository,
		IRepository<Floor, Guid> floorRepository,
		IRepository<RoomType, Guid> roomTypeRepository,
		IRepository<Room, Guid> roomRepository,
		IEducationMediumRepository educationMediumRepository,
		IRepository<Subject, Guid> subjectRepository,
		IRepository<ExamType, Guid> examTypeRepository,
		IHomeworkTypeRepository homeworkTypeRepository,
		IAssignmentTypeRepository assignmentTypeRepository,
		IRepository<HomeworkAssignmentType, Guid> homeworkAssignmentTypeRepository,
		ILogger<DemoAcademicAndInstituteSeederContributor> logger)
		: base(currentTenant, guidGenerator, unitOfWork)
	{
		_buildingRepository = buildingRepository;
		_floorRepository = floorRepository;
		_roomTypeRepository = roomTypeRepository;
		_roomRepository = roomRepository;
		_educationMediumRepository = educationMediumRepository;
		_subjectRepository = subjectRepository;
		_examTypeRepository = examTypeRepository;
		_homeworkTypeRepository = homeworkTypeRepository;
		_assignmentTypeRepository = assignmentTypeRepository;
		_homeworkAssignmentTypeRepository = homeworkAssignmentTypeRepository;
		_logger = logger;
	}

	public override async Task SeedAsync(DataSeedContext context)
	{
		await TimePhaseAsync("room seed", SeedInstituteRoomsAsync);
		await TimePhaseAsync("reference data", SeedAcademicReferenceDataAsync);
	}

	private async Task SeedInstituteRoomsAsync()
	{
		var existingBuildings = (await _buildingRepository.GetListAsync())
			.ToDictionary(x => x.BuildingName, StringComparer.OrdinalIgnoreCase);
		var existingFloors = (await _floorRepository.GetListAsync())
			.ToDictionary(x => (x.BuildingId, x.FloorNo));
		var existingRoomKeys = (await _roomRepository.GetListAsync())
			.Select(x => (x.BuildingId, x.RoomName))
			.ToHashSet();
		var existingRoomTypes = (await _roomTypeRepository.GetListAsync())
			.ToDictionary(x => x.RoomTypeName, StringComparer.OrdinalIgnoreCase);

		var roomTypesToInsert = new List<RoomType>();
		var buildingsToInsert = new List<Building>();
		var floorsToInsert = new List<Floor>();
		var roomsToInsert = new List<Room>();
		var roomTypes = EnsureRoomTypes(existingRoomTypes, roomTypesToInsert);

		foreach (var buildingSpec in BuildBuildingSpecs())
		{
			if (!existingBuildings.TryGetValue(buildingSpec.Name, out var building))
			{
				building = new Building
				{
					TenantId = CurrentTenant.Id,
					BuildingName = buildingSpec.Name,
					TotalFloors = buildingSpec.Floors.Count,
					Description = buildingSpec.Description
				};
				SetEntityId(building, GuidGenerator.Create());
				existingBuildings[buildingSpec.Name] = building;
				buildingsToInsert.Add(building);
			}

			foreach (var floorSpec in buildingSpec.Floors)
			{
				if (!existingFloors.TryGetValue((building.Id, floorSpec.FloorNo), out var floor))
				{
					floor = new Floor
					{
						FloorName = floorSpec.Name,
						FloorNo = floorSpec.FloorNo,
						TotalRoom = floorSpec.TotalRooms,
						BuildingId = building.Id
					};
					SetEntityId(floor, GuidGenerator.Create());
					existingFloors[(building.Id, floorSpec.FloorNo)] = floor;
					floorsToInsert.Add(floor);
				}

				for (var roomIndex = 1; roomIndex <= floorSpec.TotalRooms; roomIndex++)
				{
					var roomName = ResolveRoomName(building.BuildingName, floor.FloorNo, roomIndex);
					if (existingRoomKeys.Contains((building.Id, roomName)))
					{
						continue;
					}

					var typeName = ResolveRoomTypeName(building.BuildingName, floor.FloorNo, roomIndex);
					var room = new Room
					{
						TenantId = CurrentTenant.Id,
						RoomName = roomName,
						Capacity = ResolveRoomCapacity(typeName),
						BuildingId = building.Id,
						FloorId = floor.Id,
						RoomTypeId = roomTypes[typeName].Id,
						TotalBenches = typeName == "Exam Hall" ? 60 : Math.Max(12, ResolveRoomCapacity(typeName) / 2),
						BenchCapacity = 2,
						BenchesPerRow = typeName == "Exam Hall" ? 10 : 6,
						BenchesPerColumn = typeName == "Exam Hall" ? 6 : 4,
						Features = ResolveRoomFeatures(typeName)
					};
					SetEntityId(room, GuidGenerator.Create());
					existingRoomKeys.Add((building.Id, roomName));
					roomsToInsert.Add(room);
				}
			}
		}

		if (roomTypesToInsert.Count > 0)
		{
			await _roomTypeRepository.InsertManyAsync(roomTypesToInsert, autoSave: false);
		}

		if (buildingsToInsert.Count > 0)
		{
			await _buildingRepository.InsertManyAsync(buildingsToInsert, autoSave: false);
		}

		if (floorsToInsert.Count > 0)
		{
			await _floorRepository.InsertManyAsync(floorsToInsert, autoSave: false);
		}

		if (roomsToInsert.Count > 0)
		{
			await _roomRepository.InsertManyAsync(roomsToInsert, autoSave: false);
		}

		if (roomTypesToInsert.Count + buildingsToInsert.Count + floorsToInsert.Count + roomsToInsert.Count > 0)
		{
			await CurrentUnitOfWork.SaveChangesAsync();
		}

		_logger.LogInformation(
			"[DemoAcademicAndInstituteSeeder] Rooms ensured. Inserted RoomTypes={RoomTypes}, Buildings={Buildings}, Floors={Floors}, Rooms={Rooms}",
			roomTypesToInsert.Count,
			buildingsToInsert.Count,
			floorsToInsert.Count,
			roomsToInsert.Count);
	}

	private Dictionary<string, RoomType> EnsureRoomTypes(
		Dictionary<string, RoomType> existingRoomTypes,
		List<RoomType> roomTypesToInsert)
	{
		var result = new Dictionary<string, RoomType>(StringComparer.OrdinalIgnoreCase);
		foreach (var (name, shortName) in RoomTypeSpecs)
		{
			if (!existingRoomTypes.TryGetValue(name, out var roomType))
			{
				roomType = new RoomType
				{
					TenantId = CurrentTenant.Id,
					RoomTypeName = name,
					ShortName = shortName
				};
				SetEntityId(roomType, GuidGenerator.Create());
				existingRoomTypes[name] = roomType;
				roomTypesToInsert.Add(roomType);
			}

			result[name] = roomType;
		}

		return result;
	}

	private async Task SeedAcademicReferenceDataAsync()
	{
		var preload = await TimePhaseAsync(
			"reference preload",
			async () =>
			{
				var mediums = await _educationMediumRepository.GetListAsync();
				var subjects = await _subjectRepository.GetListAsync();
				var examTypes = await _examTypeRepository.GetListAsync();
				var homeworkTypes = await _homeworkTypeRepository.GetListAsync();
				var assignmentTypes = await _assignmentTypeRepository.GetListAsync();
				var homeworkAssignmentTypes = await _homeworkAssignmentTypeRepository.GetListAsync();
				return new ReferencePreload(mediums, subjects, examTypes, homeworkTypes, assignmentTypes, homeworkAssignmentTypes);
			});

		ValidateReferenceSpecs(preload.Mediums);

		var subjectKeys = preload.Subjects
			.Select(x => (x.EducationMediumId, Name: x.Name))
			.ToHashSet(GuidStringKeyComparer.Instance);
		var subjectColorKeys = preload.Subjects
			.Select(x => (x.EducationMediumId, Color: x.Color))
			.ToHashSet(GuidStringKeyComparer.Instance);
		var examTypeKeys = preload.ExamTypes
			.Select(x => (x.AcademicMediumId, Name: x.Name))
			.ToHashSet(GuidStringKeyComparer.Instance);
		var homeworkTypeKeys = preload.HomeworkTypes
			.Select(x => (x.AcademicMediumId, Name: x.Name))
			.ToHashSet(GuidStringKeyComparer.Instance);
		var assignmentTypeKeys = preload.AssignmentTypes
			.Select(x => (x.AcademicMediumId, Name: x.Name))
			.ToHashSet(GuidStringKeyComparer.Instance);
		var homeworkAssignmentTypeKeys = preload.HomeworkAssignmentTypes
			.Select(x => (x.AcademicMediumId, Name: x.Name))
			.ToHashSet(GuidStringKeyComparer.Instance);

		var subjectsToInsert = new List<Subject>();
		var examTypesToInsert = new List<ExamType>();
		var homeworkTypesToInsert = new List<HomeworkType>();
		var assignmentTypesToInsert = new List<AssignmentType>();
		var homeworkAssignmentTypesToInsert = new List<HomeworkAssignmentType>();

		foreach (var medium in preload.Mediums)
		{
			foreach (var (name, color) in BuildSubjectSpecs(medium.Name))
			{
				if (subjectKeys.Contains((medium.Id, name)))
				{
					continue;
				}

				if (!subjectColorKeys.Add((medium.Id, color)))
				{
					throw new InvalidOperationException($"Duplicate subject color '{color}' for medium '{medium.Name}'.");
				}

				subjectKeys.Add((medium.Id, name));
				subjectsToInsert.Add(new Subject(GuidGenerator.Create(), CurrentTenant.Id, name, color, medium.Id));
			}

			foreach (var (name, description) in ExamTypeSpecs)
			{
				if (!examTypeKeys.Add((medium.Id, name)))
				{
					continue;
				}

				examTypesToInsert.Add(new ExamType(GuidGenerator.Create(), CurrentTenant.Id, name, description, medium.Id));
			}

			foreach (var name in HomeworkTypeSpecs)
			{
				if (!homeworkTypeKeys.Add((medium.Id, name)))
				{
					continue;
				}

				homeworkTypesToInsert.Add(new HomeworkType(GuidGenerator.Create(), name, medium.Id, CurrentTenant.Id));
			}

			foreach (var name in AssignmentTypeSpecs)
			{
				if (!assignmentTypeKeys.Add((medium.Id, name)))
				{
					continue;
				}

				assignmentTypesToInsert.Add(new AssignmentType(GuidGenerator.Create(), name, medium.Id, CurrentTenant.Id));
			}

			foreach (var (name, type) in HomeworkAssignmentTypeSpecs)
			{
				if (!homeworkAssignmentTypeKeys.Add((medium.Id, name)))
				{
					continue;
				}

				homeworkAssignmentTypesToInsert.Add(new HomeworkAssignmentType(GuidGenerator.Create(), name, type, medium.Id, CurrentTenant.Id)
				{
					Name = name
				});
			}
		}

		await TimePhaseAsync(
			"reference inserts",
			async () =>
			{
				if (subjectsToInsert.Count > 0)
				{
					await _subjectRepository.InsertManyAsync(subjectsToInsert, autoSave: false);
				}

				if (examTypesToInsert.Count > 0)
				{
					await _examTypeRepository.InsertManyAsync(examTypesToInsert, autoSave: false);
				}

				if (homeworkTypesToInsert.Count > 0)
				{
					await _homeworkTypeRepository.InsertManyAsync(homeworkTypesToInsert, autoSave: false);
				}

				if (assignmentTypesToInsert.Count > 0)
				{
					await _assignmentTypeRepository.InsertManyAsync(assignmentTypesToInsert, autoSave: false);
				}

				if (homeworkAssignmentTypesToInsert.Count > 0)
				{
					await _homeworkAssignmentTypeRepository.InsertManyAsync(homeworkAssignmentTypesToInsert, autoSave: false);
				}

				if (subjectsToInsert.Count + examTypesToInsert.Count + homeworkTypesToInsert.Count +
				    assignmentTypesToInsert.Count + homeworkAssignmentTypesToInsert.Count > 0)
				{
					await CurrentUnitOfWork.SaveChangesAsync();
				}
			});

		_logger.LogInformation(
			"[DemoAcademicAndInstituteSeeder] Reference data ensured for Mediums={Mediums}. Inserted Subjects={Subjects}, ExamTypes={ExamTypes}, HomeworkTypes={HomeworkTypes}, AssignmentTypes={AssignmentTypes}, HomeworkAssignmentTypes={HomeworkAssignmentTypes}",
			preload.Mediums.Count,
			subjectsToInsert.Count,
			examTypesToInsert.Count,
			homeworkTypesToInsert.Count,
			assignmentTypesToInsert.Count,
			homeworkAssignmentTypesToInsert.Count);
	}

	private static IReadOnlyList<BuildingSpec> BuildBuildingSpecs() =>
	[
		new BuildingSpec(
			"Main Academic Block",
			"Primary classroom and administration block",
			[
				new FloorSpec("Ground Floor", 0, 6),
				new FloorSpec("First Floor", 1, 8),
				new FloorSpec("Second Floor", 2, 8)
			]),
		new BuildingSpec(
			"Science And Activity Block",
			"Labs, library, exam hall, and activity rooms",
			[
				new FloorSpec("Ground Floor", 0, 5),
				new FloorSpec("First Floor", 1, 6)
			])
	];

	private static string ResolveRoomName(string buildingName, int floorNo, int roomIndex)
	{
		var prefix = buildingName.StartsWith("Main", StringComparison.OrdinalIgnoreCase) ? "A" : "B";
		return $"{prefix}{floorNo}{roomIndex:D2}";
	}

	private static string ResolveRoomTypeName(string buildingName, int floorNo, int roomIndex)
	{
		if (!buildingName.StartsWith("Science", StringComparison.OrdinalIgnoreCase))
		{
			return roomIndex == 1 && floorNo == 0 ? "Staff Room" : "Classroom";
		}

		return (floorNo, roomIndex) switch
		{
			(0, 1) => "Library",
			(0, 2) => "Exam Hall",
			(_, <= 4) => "Lab",
			_ => "Classroom"
		};
	}

	private static int ResolveRoomCapacity(string typeName) => typeName switch
	{
		"Exam Hall" => 120,
		"Library" => 80,
		"Lab" => 36,
		"Staff Room" => 24,
		_ => 40
	};

	private static List<string> ResolveRoomFeatures(string typeName) => typeName switch
	{
		"Lab" => ["Projector", "Lab Tables", "Storage"],
		"Library" => ["Reading Area", "Book Shelves", "Projector"],
		"Exam Hall" => ["CCTV", "PA System", "Wide Aisles"],
		"Staff Room" => ["Workstations", "Storage", "Printer"],
		_ => ["Projector", "Whiteboard", "Fans"]
	};

	private static IReadOnlyList<(string Name, string Color)> BuildSubjectSpecs(string mediumName)
	{
		var subjects = new List<(string Name, string Color)>
		{
			("English", "#2563EB"),
			("Mathematics", "#16A34A"),
			("Science", "#DC2626"),
			("Social Science", "#D97706"),
			("Computer Science", "#7C3AED")
		};

		subjects.Add(
			mediumName.Contains("Gujarati", StringComparison.OrdinalIgnoreCase)
				? ("Gujarati", "#0D9488")
				: ("Hindi", "#DB2777"));
		return subjects;
	}

	private static void ValidateReferenceSpecs(IReadOnlyCollection<EducationMedium> mediums)
	{
		if (mediums.Count == 0)
		{
			throw new InvalidOperationException("Academic reference seed requires at least one education medium.");
		}

		foreach (var medium in mediums)
		{
			var subjects = BuildSubjectSpecs(medium.Name);
			ThrowIfDuplicate(subjects.Select(x => x.Name), $"subjects for {medium.Name}");
			ThrowIfDuplicate(subjects.Select(x => x.Color), $"subject colors for {medium.Name}");
			ThrowIfDuplicate(ExamTypeSpecs.Select(x => x.Name), "exam types");
			ThrowIfDuplicate(HomeworkTypeSpecs, "homework types");
			ThrowIfDuplicate(AssignmentTypeSpecs, "assignment types");
			ThrowIfDuplicate(HomeworkAssignmentTypeSpecs.Select(x => x.Name), "homework assignment types");
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

	private async Task TimePhaseAsync(string phaseName, Func<Task> action)
	{
		var stopwatch = Stopwatch.StartNew();
		await action();
		stopwatch.Stop();
		_logger.LogInformation("[DemoAcademicAndInstituteSeeder] {PhaseName} completed in {ElapsedMs} ms", phaseName, stopwatch.ElapsedMilliseconds);
	}

	private async Task<T> TimePhaseAsync<T>(string phaseName, Func<Task<T>> action)
	{
		var stopwatch = Stopwatch.StartNew();
		var result = await action();
		stopwatch.Stop();
		_logger.LogInformation("[DemoAcademicAndInstituteSeeder] {PhaseName} completed in {ElapsedMs} ms", phaseName, stopwatch.ElapsedMilliseconds);
		return result;
	}

	private static void SetEntityId<TEntity>(TEntity entity, Guid id)
		where TEntity : Entity<Guid>
	{
		typeof(TEntity).GetProperty(nameof(Entity<Guid>.Id))?.SetValue(entity, id);
	}

	private static readonly IReadOnlyList<(string Name, string ShortName)> RoomTypeSpecs =
	[
		("Classroom", "CLS"),
		("Lab", "LAB"),
		("Library", "LIB"),
		("Staff Room", "STF"),
		("Exam Hall", "EXM")
	];

	private static readonly IReadOnlyList<(string Name, string Description)> ExamTypeSpecs =
	[
		("Unit Test", "Short periodic assessment"),
		("Mid Term", "Mid-year term examination"),
		("Final Exam", "Final term examination"),
		("Practical", "Practical/lab assessment"),
		("Oral", "Oral assessment")
	];

	private static readonly IReadOnlyList<string> HomeworkTypeSpecs =
	[
		"Written Work",
		"Reading",
		"Worksheet",
		"Practice Set",
		"Revision"
	];

	private static readonly IReadOnlyList<string> AssignmentTypeSpecs =
	[
		"Project",
		"Research",
		"Case Study",
		"Presentation",
		"Lab Record"
	];

	private static readonly IReadOnlyList<(string Name, WorkType Type)> HomeworkAssignmentTypeSpecs =
	[
		("Daily Homework", WorkType.Homework),
		("Worksheet Homework", WorkType.Homework),
		("Class Assignment", WorkType.Assignment),
		("Project Assignment", WorkType.Assignment)
	];

	private sealed record BuildingSpec(string Name, string Description, IReadOnlyList<FloorSpec> Floors);
	private sealed record FloorSpec(string Name, int FloorNo, int TotalRooms);

	private sealed record ReferencePreload(
		List<EducationMedium> Mediums,
		List<Subject> Subjects,
		List<ExamType> ExamTypes,
		List<HomeworkType> HomeworkTypes,
		List<AssignmentType> AssignmentTypes,
		List<HomeworkAssignmentType> HomeworkAssignmentTypes);

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

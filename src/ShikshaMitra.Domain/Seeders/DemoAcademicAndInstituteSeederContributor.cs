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
	private readonly SubjectManager _subjectManager;
	private readonly IRepository<ExamType, Guid> _examTypeRepository;
	private readonly ExamTypeManager _examTypeManager;
	private readonly IHomeworkTypeRepository _homeworkTypeRepository;
	private readonly HomeworkTypeManager _homeworkTypeManager;
	private readonly IAssignmentTypeRepository _assignmentTypeRepository;
	private readonly AssignmentTypeManager _assignmentTypeManager;
	private readonly IRepository<HomeworkAssignmentType, Guid> _homeworkAssignmentTypeRepository;

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
		SubjectManager subjectManager,
		IRepository<ExamType, Guid> examTypeRepository,
		ExamTypeManager examTypeManager,
		IHomeworkTypeRepository homeworkTypeRepository,
		HomeworkTypeManager homeworkTypeManager,
		IAssignmentTypeRepository assignmentTypeRepository,
		AssignmentTypeManager assignmentTypeManager,
		IRepository<HomeworkAssignmentType, Guid> homeworkAssignmentTypeRepository)
		: base(currentTenant, guidGenerator, unitOfWork)
	{
		_buildingRepository = buildingRepository;
		_floorRepository = floorRepository;
		_roomTypeRepository = roomTypeRepository;
		_roomRepository = roomRepository;
		_educationMediumRepository = educationMediumRepository;
		_subjectRepository = subjectRepository;
		_subjectManager = subjectManager;
		_examTypeRepository = examTypeRepository;
		_examTypeManager = examTypeManager;
		_homeworkTypeRepository = homeworkTypeRepository;
		_homeworkTypeManager = homeworkTypeManager;
		_assignmentTypeRepository = assignmentTypeRepository;
		_assignmentTypeManager = assignmentTypeManager;
		_homeworkAssignmentTypeRepository = homeworkAssignmentTypeRepository;
	}

	public override async Task SeedAsync(DataSeedContext context)
	{
		// Each section is independently guarded so a partial previous run is handled correctly.
		await SeedInstituteRoomsAsync();
		await SeedAcademicReferenceDataAsync();
		await CurrentUnitOfWork.SaveChangesAsync();
	}

	/// <summary>Returns true when the given medium already has every expected assignment type.</summary>
	private async Task<bool> IsAcademicReferenceDataSeededAsync(Guid mediumId)
		=> await _assignmentTypeRepository.AnyAsync(x => x.AcademicMediumId == mediumId);

	private async Task SeedInstituteRoomsAsync()
	{
		var roomTypes = await EnsureRoomTypesAsync();
		await EnsureBuildingWithRoomsAsync(
			"Main Academic Block",
			"Primary classroom and administration block",
			[
				("Ground Floor", 0, 6),
				("First Floor", 1, 8),
				("Second Floor", 2, 8)
			],
			roomTypes);

		await EnsureBuildingWithRoomsAsync(
			"Science And Activity Block",
			"Labs, library, exam hall, and activity rooms",
			[
				("Ground Floor", 0, 5),
				("First Floor", 1, 6)
			],
			roomTypes);
	}

	private async Task<Dictionary<string, RoomType>> EnsureRoomTypesAsync()
	{
		var specs = new[]
		{
			("Classroom", "CLS"),
			("Lab", "LAB"),
			("Library", "LIB"),
			("Staff Room", "STF"),
			("Exam Hall", "EXM")
		};

		var result = new Dictionary<string, RoomType>(StringComparer.OrdinalIgnoreCase);
		foreach (var (name, shortName) in specs)
		{
			var roomType = await _roomTypeRepository.FirstOrDefaultAsync(x => x.RoomTypeName == name);
			if (roomType == null)
			{
				roomType = new RoomType
				{
					TenantId = CurrentTenant.Id,
					RoomTypeName = name,
					ShortName = shortName
				};
				await _roomTypeRepository.InsertAsync(roomType, autoSave: true);
			}

			result[name] = roomType;
		}

		return result;
	}

	private async Task EnsureBuildingWithRoomsAsync(
		string name,
		string description,
		IReadOnlyList<(string Name, int FloorNo, int TotalRooms)> floorSpecs,
		Dictionary<string, RoomType> roomTypes)
	{
		var building = await _buildingRepository.FirstOrDefaultAsync(x => x.BuildingName == name);
		if (building == null)
		{
			building = new Building
			{
				TenantId = CurrentTenant.Id,
				BuildingName = name,
				TotalFloors = floorSpecs.Count,
				Description = description
			};
			await _buildingRepository.InsertAsync(building, autoSave: true);
		}

		foreach (var floorSpec in floorSpecs)
		{
			var floor = await _floorRepository.FirstOrDefaultAsync(x =>
				x.BuildingId == building.Id &&
				x.FloorNo == floorSpec.FloorNo);
			if (floor == null)
			{
				floor = new Floor
				{
					FloorName = floorSpec.Name,
					FloorNo = floorSpec.FloorNo,
					TotalRoom = floorSpec.TotalRooms,
					BuildingId = building.Id
				};
				SetEntityId(floor, GuidGenerator.Create());
				await _floorRepository.InsertAsync(floor, autoSave: true);
			}

			for (var index = 1; index <= floorSpec.TotalRooms; index++)
			{
				await EnsureRoomAsync(building, floor, index, roomTypes);
			}
		}
	}

	private async Task EnsureRoomAsync(
		Building building,
		Floor floor,
		int roomIndex,
		Dictionary<string, RoomType> roomTypes)
	{
		var prefix = building.BuildingName.StartsWith("Main", StringComparison.OrdinalIgnoreCase) ? "A" : "B";
		var roomName = $"{prefix}{floor.FloorNo}{roomIndex:D2}";
		if (await _roomRepository.AnyAsync(x => x.RoomName == roomName && x.BuildingId == building.Id))
		{
			return;
		}

		var typeName = ResolveRoomTypeName(building.BuildingName, floor.FloorNo, roomIndex);
		var capacity = typeName switch
		{
			"Exam Hall" => 120,
			"Library" => 80,
			"Lab" => 36,
			"Staff Room" => 24,
			_ => 40
		};

		var room = new Room
		{
			TenantId = CurrentTenant.Id,
			RoomName = roomName,
			Capacity = capacity,
			BuildingId = building.Id,
			FloorId = floor.Id,
			RoomTypeId = roomTypes[typeName].Id,
			TotalBenches = typeName == "Exam Hall" ? 60 : Math.Max(12, capacity / 2),
			BenchCapacity = 2,
			BenchesPerRow = typeName == "Exam Hall" ? 10 : 6,
			BenchesPerColumn = typeName == "Exam Hall" ? 6 : 4,
			Features = ResolveRoomFeatures(typeName)
		};

		await _roomRepository.InsertAsync(room, autoSave: true);
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

	private static List<string> ResolveRoomFeatures(string typeName) => typeName switch
	{
		"Lab" => ["Projector", "Lab Tables", "Storage"],
		"Library" => ["Reading Area", "Book Shelves", "Projector"],
		"Exam Hall" => ["CCTV", "PA System", "Wide Aisles"],
		"Staff Room" => ["Workstations", "Storage", "Printer"],
		_ => ["Projector", "Whiteboard", "Fans"]
	};

	private async Task SeedAcademicReferenceDataAsync()
	{
		var mediums = await _educationMediumRepository.GetListAsync();
		foreach (var medium in mediums)
		{
			// Skip this medium if reference data was already fully seeded (idempotency guard).
			if (await IsAcademicReferenceDataSeededAsync(medium.Id))
				continue;

			await EnsureSubjectsAsync(medium.Id, medium.Name);
			await EnsureExamTypesAsync(medium.Id);
			await EnsureHomeworkTypesAsync(medium.Id);
			await EnsureAssignmentTypesAsync(medium.Id);
			await EnsureHomeworkAssignmentTypesAsync(medium.Id);

			// Flush after each medium so the next medium's idempotency check sees committed rows.
			await CurrentUnitOfWork.SaveChangesAsync();
		}
	}

	private async Task EnsureSubjectsAsync(Guid mediumId, string mediumName)
	{
		var subjects = new List<(string Name, string Color)>
		{
			("English", "#2563EB"),
			("Mathematics", "#16A34A"),
			("Science", "#DC2626"),
			("Social Science", "#D97706"),
			("Computer Science", "#7C3AED")
		};

		if (mediumName.Contains("Gujarati", StringComparison.OrdinalIgnoreCase))
		{
			subjects.Add(("Gujarati", "#0D9488"));
		}
		else
		{
			subjects.Add(("Hindi", "#DB2777"));
		}

		foreach (var (name, color) in subjects)
		{
			if (await _subjectRepository.AnyAsync(x => x.EducationMediumId == mediumId && x.Name == name))
			{
				continue;
			}

			var subject = await _subjectManager.CreateSubject(name, color, mediumId);
			await _subjectRepository.InsertAsync(subject, autoSave: false);
		}
	}

	private async Task EnsureExamTypesAsync(Guid mediumId)
	{
		var examTypes = new[]
		{
			("Unit Test", "Short periodic assessment"),
			("Mid Term", "Mid-year term examination"),
			("Final Exam", "Final term examination"),
			("Practical", "Practical/lab assessment"),
			("Oral", "Oral assessment")
		};

		foreach (var (name, description) in examTypes)
		{
			if (await _examTypeRepository.AnyAsync(x => x.AcademicMediumId == mediumId && x.Name == name))
			{
				continue;
			}

			await _examTypeManager.CreateAsync(name, description, mediumId);
		}
	}

	private async Task EnsureHomeworkTypesAsync(Guid mediumId)
	{
		foreach (var name in new[] { "Written Work", "Reading", "Worksheet", "Practice Set", "Revision" })
		{
			if (await _homeworkTypeRepository.FindByNameAndAcademicMediumIdAsync(name, mediumId) != null)
				continue;

			var homeworkType = await _homeworkTypeManager.CreateAsync(mediumId, name);
			await _homeworkTypeRepository.InsertAsync(homeworkType, autoSave: true);
		}
	}

	private async Task EnsureAssignmentTypesAsync(Guid mediumId)
	{
		foreach (var name in new[] { "Project", "Research", "Case Study", "Presentation", "Lab Record" })
		{
			if (await _assignmentTypeRepository.FindByNameAndAcademicMediumIdAsync(name, mediumId) != null)
				continue;

			var assignmentType = await _assignmentTypeManager.CreateAsync(mediumId, name);
			await _assignmentTypeRepository.InsertAsync(assignmentType, autoSave: true);
		}
	}

	private async Task EnsureHomeworkAssignmentTypesAsync(Guid mediumId)
	{
		var specs = new[]
		{
			("Daily Homework", WorkType.Homework),
			("Worksheet Homework", WorkType.Homework),
			("Class Assignment", WorkType.Assignment),
			("Project Assignment", WorkType.Assignment)
		};

		foreach (var (name, type) in specs)
		{
			if (await _homeworkAssignmentTypeRepository.AnyAsync(x =>
				    x.AcademicMediumId == mediumId &&
				    x.Name == name))
				continue;

			await _homeworkAssignmentTypeRepository.InsertAsync(
				new HomeworkAssignmentType(GuidGenerator.Create(), name, type, mediumId, CurrentTenant.Id)
				{
					Name = name
				},
				autoSave: true);
		}
	}

	private static void SetEntityId<TEntity>(TEntity entity, Guid id)
		where TEntity : Entity<Guid>
	{
		typeof(TEntity).GetProperty(nameof(Entity<Guid>.Id))?.SetValue(entity, id);
	}
}

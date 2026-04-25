using ShikshaMitra.Common;
using ShikshaMitra.Onboarding.AcademicYears;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
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
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

/// <summary>
/// Seeds student data for Class 10 divisions in the current academic year.
/// Creates diverse, realistic student profiles for testing.
/// </summary>
public class StudentSeederContributor : BaseSeederContributor
{
	private const int StudentsPerDivision = 36;
	private const int InsertBatchSize = 100;

	private readonly IRepository<Student, Guid> _studentRepository;
	private readonly IAcademicYearRepository _academicYearRepository;
	private readonly IRepository<EducationBoard, Guid> _eduBoardRepository;
	private readonly IEducationMediumRepository _educationMediumRepository;
	private readonly IEducationMediumStageRepository _educationMediumStageRepository;
	private readonly IStandardRepository _standardRepository;
	private readonly IDivisionRepository _divisionRepository;
	private readonly ILogger<StudentSeederContributor> _logger;

	// Diverse Indian names (15 boys, 15 girls)
	private readonly string[] _firstNamesBoys =
	[
		"Aarav", "Vivaan", "Aditya", "Vihaan", "Arjun", "Sai", "Arnav", "Ayaan",
		"Krishna", "Reyansh", "Ishaan", "Shaurya", "Atharv", "Advait", "Kabir"
	];

	private readonly string[] _firstNamesGirls =
	[
		"Ananya", "Aadhya", "Diya", "Saanvi", "Pari", "Aanya", "Sara", "Anvi",
		"Navya", "Kiara", "Avni", "Mira", "Riya", "Shanaya", "Myra"
	];

	private readonly string[] _middleNames =
	[
		"Kumar", "Singh", "Raj", "Mohan", "Dev", "Prasad", "Chandra", "Anil",
		"Suresh", "Prakash", "Dinesh", "Ramesh", "Vijay", "Rakesh", "Mahesh"
	];

	private readonly string[] _lastNames =
	[
		"Patel", "Sharma", "Desai", "Shah", "Mehta", "Reddy", "Gupta", "Verma",
		"Iyer", "Joshi", "Nair", "Kulkarni", "Thakur", "Pandey", "Rao"
	];

	private readonly string[] _cities = ["Surat", "Ahmedabad", "Vadodara", "Rajkot", "Bhavnagar"];
	private readonly string[] _localities = ["Athwa", "Adajan", "Vesu", "Pal", "Rander", "Parvat Patiya"];
	private readonly string[] _bloodGroups = ["A+", "B+", "O+", "AB+", "A-", "B-", "O-", "AB-"];
	private readonly string[] _castNames = ["General", "OBC", "SC", "ST", "EWS"];
	private readonly string[] _phonePrefixes = ["98", "99", "97", "96", "95", "94", "93", "92", "91", "90"];
	private readonly string[] _landmarks =
	[
		"City Hospital", "Central Park", "Bus Stand", "Railway Station",
		"Shopping Mall", "Temple", "Market", "Police Station", "Post Office",
		"School", "Garden", "Circle"
	];

	public StudentSeederContributor(
		ICurrentTenant currentTenant,
		IGuidGenerator guidGenerator,
		IUnitOfWork unitOfWork,
		IRepository<Student, Guid> studentRepository,
		IAcademicYearRepository academicYearRepository,
		IRepository<EducationBoard, Guid> eduBoardRepository,
		IEducationMediumRepository educationMediumRepository,
		IEducationMediumStageRepository educationMediumStageRepository,
		IStandardRepository standardRepository,
		IDivisionRepository divisionRepository,
		ILogger<StudentSeederContributor> logger)
		: base(currentTenant, guidGenerator, unitOfWork)
	{
		_studentRepository = studentRepository;
		_academicYearRepository = academicYearRepository;
		_eduBoardRepository = eduBoardRepository;
		_educationMediumRepository = educationMediumRepository;
		_educationMediumStageRepository = educationMediumStageRepository;
		_standardRepository = standardRepository;
		_divisionRepository = divisionRepository;
		_logger = logger;
	}

	public override async Task SeedAsync(DataSeedContext context)
	{
		// Check if students already seeded
		if (await _studentRepository.GetCountAsync() > 0)
		{
			_logger.LogInformation("[StudentSeeder] Students already exist, skipping. TenantId={TenantId}", CurrentTenant.Id);
			return;
		}

		_logger.LogInformation("[StudentSeeder] Start seeding for TenantId={TenantId}", CurrentTenant.Id);

		// Get current academic year
		var currentYear = DateTime.Now.Year;
		var currentAcademicYear = await _academicYearRepository
			.FirstOrDefaultAsync(y => y.Name.StartsWith(currentYear.ToString()));

		if (currentAcademicYear == null)
		{
			// Fallback to any academic year
			currentAcademicYear = await _academicYearRepository.FirstOrDefaultAsync();
		}

		if (currentAcademicYear == null)
		{
			_logger.LogWarning("[StudentSeeder] No academic year found. Skipping student seeding.");
			return; // No academic year found
		}

		_logger.LogInformation(
			"[StudentSeeder] Using AcademicYearId={AcademicYearId}, AcademicYearName={AcademicYearName}",
			currentAcademicYear.Id,
			currentAcademicYear.Name
		);

		// Load all required entities once to avoid nested N+1 queries.
		var boards = await _eduBoardRepository.GetListAsync(b => b.AcademicYearId == currentAcademicYear.Id);
		if (!boards.Any())
		{
			_logger.LogWarning("[StudentSeeder] No education boards found for AcademicYearId={AcademicYearId}.", currentAcademicYear.Id);
			return;
		}

		var boardIdSet = boards.Select(x => x.Id).ToHashSet();
		var allMediums = await _educationMediumRepository.GetListAsync();
		var mediums = allMediums.Where(m => boardIdSet.Contains(m.EducationBoardId)).ToList();
		var mediumIdSet = mediums.Select(x => x.Id).ToHashSet();

		var allStages = await _educationMediumStageRepository.GetListAsync();
		var stages = allStages.Where(s => mediumIdSet.Contains(s.EducationMediumId)).ToList();
		var stageIdSet = stages.Select(x => x.Id).ToHashSet();

		var allStandards = await _standardRepository.GetListAsync();
		var class10Standards = allStandards
			.Where(s => stageIdSet.Contains(s.EducationMediumStageId) &&
						s.Name.Equals("Class 10", StringComparison.OrdinalIgnoreCase))
			.ToList();
		var class10StandardIdSet = class10Standards.Select(x => x.Id).ToHashSet();

		var allDivisions = await _divisionRepository.GetListAsync();
		var divisions = allDivisions.Where(d => class10StandardIdSet.Contains(d.StandardId)).ToList();

		var mediumsByBoard = mediums.GroupBy(x => x.EducationBoardId).ToDictionary(x => x.Key, x => x.ToList());
		var stagesByMedium = stages.GroupBy(x => x.EducationMediumId).ToDictionary(x => x.Key, x => x.ToList());
		var standardsByStage = class10Standards.GroupBy(x => x.EducationMediumStageId).ToDictionary(x => x.Key, x => x.ToList());
		var divisionsByStandard = divisions.GroupBy(x => x.StandardId).ToDictionary(x => x.Key, x => x.ToList());

		_logger.LogInformation(
			"[StudentSeeder] Data loaded: Boards={Boards}, Mediums={Mediums}, Stages={Stages}, Class10Standards={Standards}, Divisions={Divisions}",
			boards.Count,
			mediums.Count,
			stages.Count,
			class10Standards.Count,
			divisions.Count
		);

		int totalStudentsCreated = 0;
		var pendingStudents = new List<Student>(InsertBatchSize);

		foreach (var board in boards)
		{
			if (!mediumsByBoard.TryGetValue(board.Id, out var mediumsForBoard))
			{
				_logger.LogDebug("[StudentSeeder] No mediums found for BoardId={BoardId}", board.Id);
				continue;
			}

			foreach (var medium in mediumsForBoard)
			{
				if (!stagesByMedium.TryGetValue(medium.Id, out var mediumStages))
				{
					continue;
				}

				foreach (var stage in mediumStages)
				{
					if (!standardsByStage.TryGetValue(stage.Id, out var standardsForStage))
					{
						continue;
					}

					foreach (var standard in standardsForStage)
					{
						if (!divisionsByStandard.TryGetValue(standard.Id, out var divisionsForStandard))
						{
							continue;
						}

						foreach (var division in divisionsForStandard)
						{
							for (int i = 0; i < StudentsPerDivision; i++)
							{
								var student = CreateStudent(
									medium.Id,
									standard.Id,
									division.Id,
									currentAcademicYear.Id,
									board.Id,
									totalStudentsCreated + 1,
									i + 1 // Roll number within division
								);

								pendingStudents.Add(student);
								totalStudentsCreated++;

								if (pendingStudents.Count >= InsertBatchSize)
								{
									await FlushStudentBatchAsync(pendingStudents, totalStudentsCreated);
								}
							}
						}
					}
				}
			}
		}

		if (pendingStudents.Count > 0)
		{
			await FlushStudentBatchAsync(pendingStudents, totalStudentsCreated);
		}

		_logger.LogInformation("[StudentSeeder] Completed seeding. TotalStudentsCreated={TotalStudentsCreated}", totalStudentsCreated);
	}

	private async Task FlushStudentBatchAsync(List<Student> pendingStudents, int totalStudentsCreated)
	{
		await _studentRepository.InsertManyAsync(pendingStudents, autoSave: false);
		await CurrentUnitOfWork.SaveChangesAsync();

		_logger.LogInformation(
			"[StudentSeeder] Checkpoint persisted. BatchSize={BatchSize}, TotalPersisted={TotalPersisted}",
			pendingStudents.Count,
			totalStudentsCreated
		);

		pendingStudents.Clear();
	}

	private Student CreateStudent(
		Guid mediumId,
		Guid standardId,
		Guid divisionId,
		Guid academicYearId,
		Guid academicBoardId,
		int globalCounter,
		int rollNo)
	{
		var random = new Random(globalCounter);
		var isGirl = globalCounter % 2 == 0;

		// Select random names
		var firstName = isGirl
			? _firstNamesGirls[globalCounter % _firstNamesGirls.Length]
			: _firstNamesBoys[globalCounter % _firstNamesBoys.Length];
		var middleName = _middleNames[globalCounter % _middleNames.Length];
		var lastName = _lastNames[globalCounter % _lastNames.Length];

		// Gender: 0 = Male, 1 = Female (adjust if your enum is different)
		var gender = (GenderEnum)(isGirl ? 1 : 0);

		var studentId = GuidGenerator.Create();

		// Age for Class 10 students: 14-16 years
		var age = 14 + (globalCounter % 3); // Varies between 14, 15, 16
		var dateOfBirth = DateTimeOffset.Now.AddYears(-age).AddDays(random.Next(-180, 180));

		// Create student with Admission status (already enrolled and approved)
		var student = new Student(
			id: studentId,
			tenantId: CurrentTenant.Id,
			firstName: firstName,
			middleName: middleName,
			lastName: lastName,
			educationYearId: academicYearId,
			educationBoardId: academicBoardId,
			educationMediumId: mediumId,
			educationMediumStageId: Guid.Empty, // Can be set if needed
			standardId: standardId,
			divisionId: divisionId,
			grNo: 10000 + globalCounter, // GR numbers starting from 10001
			rollNo: rollNo,
			status: StudentLifeCycleStatusEnum.Admission
		);

		// Create additional details
		var additionalDetail = new StudentAdditionalDetail(
			id: studentId,
			tenantId: CurrentTenant.Id,
			dateOfBirthday: dateOfBirth,
			gender: gender,
			dateOfInquiry: null,
			inquiryNo: string.Empty,
			inquiryPreferenceId: null,
			previousSchool: null
		);

		var city = _cities[globalCounter % _cities.Length];
		var locality = _localities[globalCounter % _localities.Length];

		// Religion: 0=Hindu, 1=Muslim, 2=Jain, 3=Christian, 4=Sikh (adjust if different)
		var religion = (ReligionEnum)(globalCounter % 5);

		// Cast categories: 0=General, 1=OBC, 2=SC, 3=ST, 4=EWS
		var castCategory = (CastCategoryEnum)(globalCounter % 5);
		// Mother tongue: 0=Gujarati, 1=Hindi, 2=English, 3=Marathi (adjust if different)
		var motherTongue = (MotherTongueEnum)(globalCounter % 4);

		additionalDetail.UpdateGeneralInformation(
			dateOfBirth,
			gender,
			null, // ProfilePictureUrl
			GenerateAadharNumber(random),
			null, // ApparId
			null, // PanNo
			null, // PenNo
			religion,
			_castNames[(int)castCategory],
			castCategory,
			"Indian",
			city,
			motherTongue,
			"ABC Public School", // Previous school
			"Class 9", // Last class passed
			StudentRegistrationStateEnum.BankDetails, // Complete registration state
			null,
			"1234"
		);

		// Create contact details
		var contactDetail = new StudentContactDetail(
			id: studentId,
			tenantId: CurrentTenant.Id,
			primaryContactNo: GeneratePhoneNumber(random),
			secondaryContactNo: random.Next(0, 3) > 0 ? GeneratePhoneNumber(random) : null,
			email: $"{firstName.ToLower()}.{lastName.ToLower()}{globalCounter}@student.test.com",
			address1: $"{random.Next(1, 500)}, {locality}",
			address2: $"Near {GenerateLandmark(random)}",
			address3: null,
			city: city,
			state: new State("Gujarat", "GJ"),
			country: new Country("India", "IN"),
			pincode: $"3950{random.Next(10, 100)}"
		);

		// Create medical record
		var medicalRecord = new StudentMedicalRecord(studentId, CurrentTenant.Id);
		var bloodGroup = _bloodGroups[globalCounter % _bloodGroups.Length];

		medicalRecord.UpdateMedicalRecords(
			weightKg: 45 + random.Next(-10, 15), // 35-60 kg
			heightCm: 155 + random.Next(-10, 15), // 145-170 cm
			bodyMassIndex: null,
			pulseRate: 70 + random.Next(-10, 15),
			hemoglobin: 12.0m + (decimal)(random.NextDouble() * 3), // 12-15 g/dL
			allergies: random.Next(0, 10) > 7 ? "Dust, Pollen" : null,
			childImmunization: "Complete as per age",
			covidVaccinationDoses: 2, // Class 10 students have 2 doses
			immunizationRemarks: "All vaccinations up to date",
			bloodGroup: bloodGroup,
			chronicConditions: random.Next(0, 20) > 18 ? "Asthma" : null,
			currentMedications: null
		);

		// Set navigation properties using reflection
		student.GetType().GetProperty("AdditionalDetail")?.SetValue(student, additionalDetail);
		student.GetType().GetProperty("ContactDetail")?.SetValue(student, contactDetail);
		student.GetType().GetProperty("MedicalRecord")?.SetValue(student, medicalRecord);

		// Approve admission (sets status to Approved)
		student.ApproveAdmission();

		return student;
	}

	#region Helper Methods

	private string GenerateAadharNumber(Random random)
	{
		return $"{random.Next(1000, 9999)} {random.Next(1000, 9999)} {random.Next(1000, 9999)}";
	}

	private string GeneratePhoneNumber(Random random)
	{
		var prefix = _phonePrefixes[random.Next(_phonePrefixes.Length)];
		return $"+91-{prefix}{random.Next(10000000, 99999999)}";
	}

	private string GenerateLandmark(Random random)
	{
		return _landmarks[random.Next(_landmarks.Length)];
	}

	#endregion
}

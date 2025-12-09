using ShikshaMitra.Onboarding.AcademicYears;
using ShikshaMitra.Students;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Linq;
using System.Threading.Tasks;
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
	private readonly IRepository<Student, Guid> _studentRepository;
	private readonly IAcademicYearRepository _academicYearRepository;
	private readonly IEducationMediumRepository _educationMediumRepository;
	private readonly IStandardRepository _standardRepository;
	private readonly IDivisionRepository _divisionRepository;

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

	public StudentSeederContributor(
		ICurrentTenant currentTenant,
		IGuidGenerator guidGenerator,
		IUnitOfWork unitOfWork,
		IRepository<Student, Guid> studentRepository,
		IAcademicYearRepository academicYearRepository,
		IEducationMediumRepository educationMediumRepository,
		IStandardRepository standardRepository,
		IDivisionRepository divisionRepository)
		: base(currentTenant, guidGenerator, unitOfWork)
	{
		_studentRepository = studentRepository;
		_academicYearRepository = academicYearRepository;
		_educationMediumRepository = educationMediumRepository;
		_standardRepository = standardRepository;
		_divisionRepository = divisionRepository;
	}

	public override async Task SeedAsync(DataSeedContext context)
	{
		// Check if students already seeded
		if (await _studentRepository.GetCountAsync() > 0)
			return;

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
			return; // No academic year found
		}

		// Get all mediums
		var mediums = await _educationMediumRepository.GetListAsync();

		int totalStudentsCreated = 0;

		// Seed students only for Class 10
		foreach (var medium in mediums)
		{
			// Get Class 10 standard for this medium directly from repository
			var allStandards = await _standardRepository.GetListAsync();
			var class10Standards = allStandards
				.Where(s => s.EducationMediumStage.EducationMediumId == medium.Id 
				            && s.Name == "Class 10")
				.ToList();

			foreach (var standard in class10Standards)
			{
				// Get all divisions for Class 10
				var divisions = await _divisionRepository.GetListAsync(d => d.StandardId == standard.Id);

				foreach (var division in divisions)
				{
					// Create 36 students per division (80% of 45 capacity)
					var studentsInDivision = 36;
					
					for (int i = 0; i < studentsInDivision; i++)
					{
						await CreateStudentAsync(
							medium.Id,
							standard.Id,
							division.Id,
							currentAcademicYear.Id,
							totalStudentsCreated + 1,
							i + 1 // Roll number within division
						);
						
						totalStudentsCreated++;
					}
				}
			}
		}

		await CurrentUnitOfWork.SaveChangesAsync();
	}

	private async Task CreateStudentAsync(
		Guid mediumId,
		Guid standardId,
		Guid divisionId,
		Guid academicYearId,
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
			educationMediumId: mediumId,
			standardId: standardId,
			divisionId: divisionId,
			academicYearId: academicYearId,
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
			inquiryPreferenceId: null
		);

		var city = _cities[globalCounter % _cities.Length];
		var locality = _localities[globalCounter % _localities.Length];
		
		// Religion: 0=Hindu, 1=Muslim, 2=Jain, 3=Christian, 4=Sikh (adjust if different)
		var religion = (ReligionEnum)(globalCounter % 5);
		
		// Cast categories: 0=General, 1=OBC, 2=SC, 3=ST, 4=EWS
		var castCategory = (CastCategoryEnum)(globalCounter % 5);
		var castNames = new[] { "General", "OBC", "SC", "ST", "EWS" };
		
		// Mother tongue: 0=Gujarati, 1=Hindi, 2=English, 3=Marathi (adjust if different)
		var motherTongue = (MotherTongueEnum)(globalCounter % 4);
		
		additionalDetail.UpdateGeneralInformation(
			dateOfBirth,
			gender,
			null, // ProfilePictureUrl
			GenerateAadharNumber(random),
			null, // ApparId
			null, // PanNo
			religion,
			castNames[(int)castCategory],
			castCategory,
			"Indian",
			city,
			motherTongue,
			"ABC Public School", // Previous school
			"Class 9", // Last class passed
			StudentRegistrationStateEnum.BankDetails // Complete registration state
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
			state: "Gujarat",
			country: "India",
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

		await _studentRepository.InsertAsync(student, autoSave: false);
	}

	#region Helper Methods

	private string GenerateAadharNumber(Random random)
	{
		return $"{random.Next(1000, 9999)} {random.Next(1000, 9999)} {random.Next(1000, 9999)}";
	}

	private string GeneratePhoneNumber(Random random)
	{
		var prefixes = new[] { "98", "99", "97", "96", "95", "94", "93", "92", "91", "90" };
		var prefix = prefixes[random.Next(prefixes.Length)];
		return $"+91-{prefix}{random.Next(10000000, 99999999)}";
	}

	private string GenerateLandmark(Random random)
	{
		var landmarks = new[] 
		{
			"City Hospital", "Central Park", "Bus Stand", "Railway Station",
			"Shopping Mall", "Temple", "Market", "Police Station", "Post Office",
			"School", "Garden", "Circle"
		};
		return landmarks[random.Next(landmarks.Length)];
	}

	#endregion
}

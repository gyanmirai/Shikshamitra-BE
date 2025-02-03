using ShikshaMitra.Common;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using ShikshaMitra.Students.StudentEnrollments;
using System;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

public class StudentSeederContributor : BaseSeederContributor
{
	private readonly IStudentEnrollmentRepository _studentEnrollmentRepository;
	private readonly StudentEnrollmentManager _studentEnrollmentManager;
	private readonly IInquiryPreferenceRepository _inquiryPreferenceRepository;

	public StudentSeederContributor(
		ICurrentTenant currentTenant,
		IGuidGenerator guidGenerator,
		IUnitOfWork unitOfWork,
		IStudentEnrollmentRepository studentEnrollmentRepository,
		StudentEnrollmentManager studentEnrollmentManager,
		IInquiryPreferenceRepository inquiryPreferenceRepository)
		: base(currentTenant, guidGenerator, unitOfWork)
	{
		_studentEnrollmentRepository = studentEnrollmentRepository;
		_studentEnrollmentManager = studentEnrollmentManager;
		_inquiryPreferenceRepository = inquiryPreferenceRepository;
	}

	public override async Task SeedAsync(DataSeedContext context)
	{
		var tenantId = CurrentTenant.Id;

		// Create Inquiry Preference.
		var inquiryPreference = await _inquiryPreferenceRepository.InsertAsync(new InquiryPreference(
			GuidGenerator.Create(),
			tenantId,
			"Online"
		));

		// Retrieve academic-related IDs from the context.
		if (!context.Properties.ContainsKey("AcademicYearId") ||
			!context.Properties.ContainsKey("EducationBoardId") ||
			!context.Properties.ContainsKey("EducationMediumId") ||
			!context.Properties.ContainsKey("StandardId"))
		{
			return;
		}

		var academicYearId = (Guid) context.Properties["AcademicYearId"];
		var educationBoardId = (Guid) context.Properties["EducationBoardId"];
		var educationMediumId = (Guid) context.Properties["EducationMediumId"];
		var standardId = (Guid) context.Properties["StandardId"];

		// Create a student inquiry.
		var newStudentInquiry = await _studentEnrollmentManager.CreateStudentInquiryAsync(
			studentEnrollmentState: StudentEnrollmentState.Inquiry,
			formNumber: null,
			firstName: "John",
			middleName: "Michael",
			lastName: "Doe",
			previousSchoolName: "Springfield Public School",
			dateOfBirth: new DateTimeOffset(new DateTime(2015, 3, 15)),
			dateOfInquiry: new DateTimeOffset(DateTime.UtcNow),
			genderType: GenderType.Male,
			parentContactNumber: "+91-9988776655",
			alternativeContactNumber: "+91-9876543210",
			addressLine1: "Trimandir",
			addressLine2: "Trimandir",
			addressLine3: null,
			country: "India",
			city: "Adalaj",
			state: "Gujarat",
			pinCode: "382421",
			inquiryStatusType: InquiryStatusType.Pending,
			academicYearId: academicYearId,
			educationBoardId: educationBoardId,
			educationMediumId: educationMediumId,
			standardId: standardId,
			inquiryPreferenceId: inquiryPreference.Id
		);

		await _studentEnrollmentRepository.InsertAsync(newStudentInquiry);
	}
}

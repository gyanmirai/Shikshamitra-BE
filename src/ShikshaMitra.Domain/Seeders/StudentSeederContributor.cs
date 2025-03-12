using ShikshaMitra.Common;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using ShikshaMitra.Students.StudentEnrollments;
using System;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;

namespace ShikshaMitra.Seeders;

public class StudentSeederContributor(
	ICurrentTenant currentTenant,
	IGuidGenerator guidGenerator,
	IUnitOfWork unitOfWork,
	IStudentEnrollmentRepository studentEnrollmentRepository,
	IStudentRepository studentRepository,
	StudentEnrollmentManager studentEnrollmentManager,
	IInquiryPreferenceRepository inquiryPreferenceRepository) : BaseSeederContributor(currentTenant, guidGenerator, unitOfWork)
{
	public override async Task SeedAsync(DataSeedContext context)
	{
		Guid tenantId = (Guid)CurrentTenant.Id!;

		// Create Inquiry Preference.
		var inquiryPreference = await inquiryPreferenceRepository.InsertAsync(new InquiryPreference(
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
		var educationMediumStageId = (Guid) context.Properties["EducationMediumStageId"];
		var standardId = (Guid) context.Properties["StandardId"];
		var divisionId = (Guid) context.Properties["DivisionId"];

		// Create a student inquiry.
		var newStudentInquiry = await studentEnrollmentManager.CreateStudentInquiryAsync(
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
			country: new Country("India", "In"),
			city: "Adalaj",
			state: new State("Gujarat", "GJ"),
			pinCode: "382421",
			inquiryStatusType: FollowUpStatusType.Pending,
			academicYearId: academicYearId,
			educationBoardId: educationBoardId,
			educationMediumId: educationMediumId,
			standardId: standardId,
			inquiryPreferenceId: inquiryPreference.Id
		);

		await studentEnrollmentRepository.InsertAsync(newStudentInquiry);

		// Create Student
		var student = new Student(
			GuidGenerator.Create(),
			tenantId: tenantId,
			name: "Hiren Student",
			academicYearId: academicYearId,
			educationBoardId: educationBoardId,
			educationMediumId: educationMediumId,
			educationMediumStageId: educationMediumStageId,
			standardId: standardId,
			divisionId: divisionId,
			grNoRecord: 1,
			rollNo: 1
			);

		await studentRepository.InsertAsync(student);

	}
}

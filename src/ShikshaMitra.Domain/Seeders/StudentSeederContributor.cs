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
using ShikshaMitra.Students.StudentEnrollments.Registration;
using System.Collections.Generic;

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
		Guid tenantId = (Guid) CurrentTenant.Id!;

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
			!context.Properties.ContainsKey("EducationMediumStageId") ||
			!context.Properties.ContainsKey("StandardId") ||
			!context.Properties.ContainsKey("DivisionId")
			)
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
			email:"test@gmail.com",
			previousSchoolName: "Springfield Public School",
			dateOfBirth: new DateTimeOffset(new DateTime(2015, 3, 15)),
			dateOfInquiry: new DateTimeOffset(DateTime.UtcNow),
			genderType: GenderType.Male,
			primaryContactNumber: "+91-9988776655",
			secondaryContactNumber: "+91-9876543210",
			addressLine1: "Trimandir",
			addressLine2: "Trimandir",
			addressLine3: null,
			country: new Country("India", "In"),
			pan: "ABCDE1234F",
			apparId: "123456",
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

		// Create default value objects for new Student properties using the correct parameters
		var fatherDetails = new StudentContactDetails(
			name: "Dineshbhai Vadher",
			dateOfBirth: new DateTimeOffset(new DateTime(1975, 5, 15)),
			occupation: "Business",
			qualification: "Graduate",
			contactNo: "+91-9876543210",
			email: "dinesh.vadher@example.com",
			passOutYear: 1996,
			officeNo: "+91-0792223344",
			officeAddress1: "123 Business Park, Ahmedabad",
			officeAddress2: "123 Business Park, Ahmedabad",
			officeAddress3: "123 Business Park, Ahmedabad",
			designation: "Owner",
			organisation: "Vadher Enterprises",
			aadhaarNo: "1234 5678 9012",
			remarks: null,
			address1: "123 Main Street, Ahmedabad",
			address2: "123 Main Street, Ahmedabad",
			address3: "123 Main Street, Ahmedabad"
		);

		var motherDetails = new StudentContactDetails(
			name: "Nathiben Vadher",
			dateOfBirth: new DateTimeOffset(new DateTime(1978, 8, 20)),
			occupation: "Homemaker",
			qualification: "Graduate",
			contactNo: "+91-9876543211",
			email: "nathiben.vadher@example.com",
			passOutYear: 1998,
			officeNo: null,
			officeAddress1: null,
			officeAddress2: null,
			officeAddress3: null,
			designation: null,
			organisation: null,
			aadhaarNo: "2345 6789 0123",
			remarks: null,
			address1: "123 Main Street, Ahmedabad",
			address2: "123 Main Street, Ahmedabad",
			address3: "123 Main Street, Ahmedabad"
		);

		var guardianDetails = new StudentContactDetails(
			name: null,
			dateOfBirth: null,
			occupation: null,
			qualification: null,
			contactNo: null,
			email: null,
			passOutYear: null,
			officeNo: null,
			officeAddress1: null,
			officeAddress2: null,
			officeAddress3: null,
			designation: null,
			organisation: null,
			aadhaarNo: null,
			remarks: null,
			address1: null,
			address2: null,
			address3: null
		);

		var studentCommunicationDetails = new StudentCommunicationDetails(
			primaryContactNo: "+91-9876543210",
			parentContactNo: "+91-9876543210",
			parentEmail: "dinesh.vadher@example.com",
			studentContactNo: "+91-8765432100",
			studentEmail: "hiren.vadher@example.com",
			whatsAppNo: "+91-9876543210",
			localAddressLine1: "123 Main Street",
			localAddressLine2: "Near City Center",
			localAddressLine3: null,
			localCountryName: "India",
			localCountryIsoCode: "IN",
			localStateName: "Gujarat",
			localStateIsoCode: "GJ",
			localCity: "Ahmedabad",
			localPinCode: "380001",
			permanentAddressLine1: "123 Main Street",
			permanentAddressLine2: "Near City Center",
			permanentAddressLine3: null,
			permanentCountryIsoCode: "IN",
			permanentCountryName: "India",
			permanentStateIsoCode: "GJ",
			permanentStateName: "Gujarat",
			permanentCity: "Ahmedabad",
			permanentPinCode: "380001",
			emergencyAddressLine1: "456 Other Street",
			emergencyAddressLine2: "Near Hospital",
			emergencyAddressLine3: null,
			emergencyCountry: "India",
			emergencyState: "Gujarat",
			emergencyCity: "Ahmedabad",
			emergencyPinCode: "380001",
			remarks: null
		);

		var studentAdditionalDetails = new StudentAdditionalDetails(
			category: Category.General,
			rightToEducation: false,
			bPLStudent: false,
			bPLCardNo: null,
			personWithDisability: false,
			secondLanguage: "Hindi",
			emergencyContactNo: "+91-9876543222",
			singleParentChild: false,
			singleChild: true,
			sponsorName: null
		);

		var studentMedicalRecords = new StudentMedicalRecords(
			weight: 65.0f,
			height: 175.0f,
			bodyMassIndex: 21.2f,
			haemoglobin: 14.0f,
			allergies: "None",
			covidVaccination: CovidVaccineDoses.FirstDose,
			pulseRate: 72.0f,
			childImmunisation: "Complete",
			immunisationRemarks: null
		);

		var previousSchoolDetails = new PreviousSchoolDetails(
			previousSchoolName: "ABC School",
			previousSchoolAddress: "XYZ Street, Ahmedabad",
			previousSchoolBoard: "CBSE",
			previousSchoolMedium: "English",
			tcNo: "TC123456",
			lastClassPassed: "10th",
			percentage: 85.5f
		);

		var parentBankDetails = new ParentBankDetails(
			bankName: "State Bank of India",
			branchName: "City Branch",
			accountHolderName: "Dineshbhai Vadher",
			accountNumber: "1234567890",
			ifscCode: "SBIN0001234"
		);

		// Create Student with all required properties
		var student = new Student(
			// Basic information
			id: GuidGenerator.Create(),
			tenantId: tenantId,
			formNumber: "F2025001",
			firstName: "Hiren",
			middleName: "Dineshbhai",
			lastName: "Vadher",
			dateOfBirth: new DateTimeOffset(new DateTime(2000, 1, 15)),
			genderType: GenderType.Male,
			email: "hiren.vadher@example.com",
			parentContactNumber: "+91-9876543210",
			alternativeContactNumber: "+91-9876543211",
			profilePicture: null,

			// Address details
			addressLine1: "123 Main Street",
			addressLine2: "Near City Center",
			addressLine3: null,
			country: new Country("India", "In"),
			state: new State("Gujarat", "GJ"),
			city: "Ahmedabad",
			pinCode: "380001",

			// Personal details
			aadhaarNo: "1234 5678 9012",
			religion: "Hindu",
			familyCast: "Patel",
			castType: CastType.General,
			motherTongue: "Gujarati",
			nationality: "Indian",
			birthPlace: "Ahmedabad",
			remarks: "Excellent student",
			userId: null,

			// Academic details
			academicYearId: academicYearId,
			educationBoardId: educationBoardId,
			educationMediumId: educationMediumId,
			educationMediumStageId: educationMediumStageId,
			standardId: standardId,
			divisionId: divisionId,
			grNoRecord: 1,
			rollNo: 1,

			// Family details
			fatherDetails: fatherDetails,
			motherDetails: motherDetails,
			guardianDetails: guardianDetails,

			// Additional details
			studentCommunicationDetails: studentCommunicationDetails,
			studentAdditionalDetails: studentAdditionalDetails,
			studentMedicalRecords: studentMedicalRecords,
			previousSchoolDetails: previousSchoolDetails,
			parentBankDetails: parentBankDetails,
			documents: new List<StudentDocument>()
		);


		await studentRepository.InsertAsync(student);

	}
}

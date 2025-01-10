using AutoMapper;
using ShikshaMitra.Students.Attendance.settings;
using ShikshaMitra.Students.Attendance.Settings;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Inquiry;
using ShikshaMitra.Students.StudentEnrollments.Registration;
using ShikshaMitra.Students.StudentEnrollments.Settings;

namespace ShikshaMitra.Students;

public class StudentMapperProfile : Profile
{
	public StudentMapperProfile()
	{
		// Mapping for Student
		CreateMap<Student, StudentDto>();
		CreateMap<CreateUpdateStudentDto, Student>();

		CreateMap<Address, AddressDto>().ReverseMap();
		CreateMap<StudentParentDetails, StudentParentDetailsDto>().ReverseMap();
		CreateMap<CommunicationDetails, CommunicationDetailsDto>().ReverseMap();
		CreateMap<AdmissionDetails, AdmissionDetailsDto>().ReverseMap();

		#region Student Enrollment

		CreateMap<StudentEnrollment, StudentEnrollmentDto>();
		CreateMap<CreateUpdateStudentEnrollmentDto, StudentEnrollment>();

		CreateMap<StudentContactDetails, StudentContactDetailsDto>().ReverseMap();
		CreateMap<StudentCommunicationDetails, StudentCommunicationDetailsDto>().ReverseMap();
		CreateMap<StudentAdditionalDetails, StudentAdditionalDetailsDto>().ReverseMap();
		CreateMap<StudentMedicalRecords, StudentMedicalRecordsDto>().ReverseMap();
		CreateMap<PreviousSchoolDetails, PreviousSchoolDetailsDto>().ReverseMap();
		CreateMap<ParentBankDetails, ParentBankDetailsDto>();

		// Inquiry
		CreateMap<StudentInquiry, StudentInquiryDto>();
		CreateMap<CreateUpdateStudentInquiryDto, StudentInquiry>();

		#region settings
		
		// Document settings
		CreateMap<StudentDocumentSetting, StudentDocumentSettingDto>();
		CreateMap<CreateUpdateStudentDocumentSettingDto, StudentDocumentSetting>();
		
		// Inquiry Preference
		CreateMap<InquiryPreference, InquiryPreferenceDto>();
		CreateMap<CreateUpdateInquiryPreferenceDto, InquiryPreference>();
		
		#endregion

		#endregion

		#region Attendance

		#region Settings

		CreateMap<AttendanceType, AttendanceTypeDto>();
		CreateMap<CreateUpdateAttendanceTypeDto, AttendanceType>();
		CreateMap<StudentAttendanceRule, StudentAttendanceRuleDto>();
		CreateMap<CreateUpdateStudentAttendanceRuleDto, StudentAttendanceRule>();

		#endregion
		
		#endregion
		
	}
}
using AutoMapper;
using ShikshaMitra.Students.Attendance.settings;
using ShikshaMitra.Students.Attendance.Settings;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Registration;
using ShikshaMitra.Students.StudentEnrollments.Settings;

namespace ShikshaMitra.Students;

public class StudentMapperProfile : Profile
{
	public StudentMapperProfile()
	{
		#region Student Enrollment

		CreateMap<StudentEnrollments.StudentEnrollment, StudentEnrollmentDto>();
		CreateMap<CreateUpdateStudentEnrollmentDto, StudentEnrollments.StudentEnrollment>();
		CreateMap<StudentBasicDetails, StudentBasicDetailsDto>().ReverseMap();
		CreateMap<UpdateStudentBasicDetailsDto, StudentBasicDetails>();
		CreateMap<StudentContactDetails, StudentContactDetailsDto>().ReverseMap();
		CreateMap<StudentCommunicationDetails, StudentCommunicationDetailsDto>().ReverseMap();
		CreateMap<StudentAdditionalDetails, StudentAdditionalDetailsDto>().ReverseMap();
		CreateMap<StudentMedicalRecords, StudentMedicalRecordsDto>().ReverseMap();
		CreateMap<PreviousSchoolDetails, PreviousSchoolDetailsDto>().ReverseMap();
		CreateMap<ParentBankDetails, ParentBankDetailsDto>().ReverseMap();
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
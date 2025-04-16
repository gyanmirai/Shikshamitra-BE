using AutoMapper;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.Students.Attendance.settings;
using ShikshaMitra.Students.Attendance.Settings;
using ShikshaMitra.Students.FollowUps;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.EntranceExam;
using ShikshaMitra.Students.StudentEnrollments.FollowUps;
using ShikshaMitra.Students.StudentEnrollments.Registration;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.Students.StudentRecords.StudentGroups;

namespace ShikshaMitra.Students;

public class StudentMapperProfile : Profile
{
	public StudentMapperProfile()
	{
		#region Student Enrollment

		CreateMap<StudentEnrollments.StudentEnrollment, StudentEnrollmentDto>().ReverseMap();
		CreateMap<CreateUpdateStudentEnrollmentDto, StudentEnrollments.StudentEnrollment>();
		//CreateMap<StudentBasicDetails, StudentBasicDetailsDto>().ReverseMap();
		//CreateMap<UpdateStudentBasicDetailsDto, StudentBasicDetails>();
		CreateMap<StudentContactDetails, StudentContactDetailsDto>().ReverseMap();
		CreateMap<StudentCommunicationDetails, StudentCommunicationDetailsDto>().ReverseMap();
		CreateMap<StudentAdditionalDetails, StudentAdditionalDetailsDto>().ReverseMap();
		CreateMap<StudentMedicalRecords, StudentMedicalRecordsDto>().ReverseMap();
		CreateMap<PreviousSchoolDetails, PreviousSchoolDetailsDto>().ReverseMap();
		CreateMap<ParentBankDetails, ParentBankDetailsDto>().ReverseMap();

		CreateMap<FollowUpMessage, FollowUpMessageDto>().ReverseMap();

		#region Entrance Exam
		CreateMap<StudentEnrollments.EntranceExam.EntranceExam, EntranceExamDto>();
		CreateMap<CreateUpdateEntranceExamDto, StudentEnrollments.EntranceExam.EntranceExam>();
		#endregion

		#region settings
		// Document settings
		CreateMap<StudentDocumentSetting, StudentDocumentSettingDto>();
		CreateMap<CreateUpdateStudentDocumentSettingDto, StudentDocumentSetting>();

		// Inquiry Preference
		CreateMap<InquiryPreference, InquiryPreferenceDto>();
		CreateMap<CreateUpdateInquiryPreferenceDto, InquiryPreference>();

		// Entrance ExamSlot
		CreateMap<EntranceExamSlot, EntranceExamSlotDto>();
		CreateMap<CreateUpdateEntranceExamSlotDto, EntranceExamSlot>();
		#endregion

		#endregion

		#region Attendance
		CreateMap<StudentAttendanceDto, StudentAttendance>().ReverseMap();
		#region Settings

		CreateMap<AttendanceType, AttendanceTypeDto>();
		CreateMap<CreateUpdateAttendanceTypeDto, AttendanceType>();
		CreateMap<StudentAttendanceRule, StudentAttendanceRuleDto>();
		CreateMap<CreateUpdateStudentAttendanceRuleDto, StudentAttendanceRule>();

		#endregion

		#endregion


		CreateMap<Student, StudentDto>().ReverseMap();

		CreateMap<StudentGroup, StudentGroupDto>().ReverseMap();
		CreateMap<CreateUpdateStudentGroupDto, StudentGroup>();


		CreateMap<StudentGroupMember, StudentGroupMemberDto>()
	.ForMember(dest => dest.StudentName, opt => opt.MapFrom(src =>
		src.Student != null ? $"{src.Student.FirstName} {src.Student.LastName}" : string.Empty))
	.ForMember(dest => dest.StudentGroupName, opt => opt.MapFrom(src =>
		src.StudentGroup != null ? src.StudentGroup.Name : string.Empty));

	}
}
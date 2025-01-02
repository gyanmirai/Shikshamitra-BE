using AutoMapper;
using ShikshaMitra.Students.Attendance.settings;
using ShikshaMitra.Students.Attendance.Settings;
using ShikshaMitra.Students.studentenrollment.Inquiry;
using ShikshaMitra.Students.StudentEnrollment.Inquiry;

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


		#region Attendance

		#region Settings

		CreateMap<AttendanceType, AttendanceTypeDto>();
		CreateMap<CreateUpdateAttendanceTypeDto, AttendanceType>();
		CreateMap<StudentAttendanceRule, StudentAttendanceRuleDto>();
		CreateMap<CreateUpdateStudentAttendanceRuleDto, StudentAttendanceRule>();

		#endregion
		
		#endregion

		#region Inquiry

		CreateMap<StudentInquiry, StudentInquiryDto>();
		CreateMap<CreateUpdateStudentInquiryDto, StudentInquiry>();

		#endregion
	}
}
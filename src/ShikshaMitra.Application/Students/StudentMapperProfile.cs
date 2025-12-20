using AutoMapper;
using ShikshaMitra.Common;
using ShikshaMitra.Students.Attendance.Settings;
using ShikshaMitra.Students.Attendances.Settings;
using ShikshaMitra.Students.StudentDetails;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using System;
using System.Linq;

namespace ShikshaMitra.Students;

public class StudentMapperProfile : Profile
{
	public StudentMapperProfile()
	{
		// Entrance Exam Slot mappings
		CreateMap<EntranceExamSlot, EntranceExamSlotDto>()
			.ForMember(dest => dest.Date,
				opt => opt.MapFrom(src => src.StartDate.Date))
			.ForMember(dest => dest.StartTime,
				opt => opt.MapFrom(src => src.StartDate.TimeOfDay))
			.ForMember(dest => dest.EndTime,
				opt => opt.MapFrom(src => src.EndDate.TimeOfDay))
			.ForMember(dest => dest.Duration,
				opt => opt.MapFrom(src => (int)(src.EndDate - src.StartDate).TotalMinutes))
			.ForMember(dest => dest.EnrolledStudents,
				opt => opt.Ignore()) // Will be set manually in app service
			.ForMember(dest => dest.AvailableSeats,
				opt => opt.Ignore()); // Will be set manually in app service

		CreateMap<EntranceExamSlot, EntranceExamSlotDetailDto>()
			.IncludeBase<EntranceExamSlot, EntranceExamSlotDto>()
			.ForMember(dest => dest.CreatedTime,
				opt => opt.MapFrom(src => src.CreationTime))
			.ForMember(dest => dest.LastModifiedTime,
				opt => opt.MapFrom(src => src.LastModificationTime));

		CreateMap<EntranceExamSlot, AvailableExamSlotDto>()
			.ForMember(dest => dest.Date,
				opt => opt.MapFrom(src => src.StartDate.Date))
			.ForMember(dest => dest.StartTime,
				opt => opt.MapFrom(src => src.StartDate.TimeOfDay))
			.ForMember(dest => dest.EndTime,
				opt => opt.MapFrom(src => src.EndDate.TimeOfDay))
			.ForMember(dest => dest.AvailableSeats,
				opt => opt.Ignore()) // Will be set manually
			.ForMember(dest => dest.DisplayText,
				opt => opt.Ignore()); // Will be set manually


		// Student Leave Type mappings
		CreateMap<StudentLeaveType, StudentLeaveTypeDto>();
		CreateMap<CreateUpdateStudentLeaveTypeDto, StudentLeaveType>();



		// Map Student aggregate to StudentInquiryDto (used in inquiry create/update flows)
		CreateMap<Student, StudentInquiryDto>()
			.ForMember(dest => dest.StudentId, opt => opt.MapFrom(src => src.Id))
			.ForMember(dest => dest.InquiryNo, opt => opt.MapFrom(src => src.AdditionalDetail != null ? src.AdditionalDetail.InquiryNo : null))
			.ForMember(dest => dest.StudentName, opt => opt.MapFrom(src =>
				$"{src.FirstName} {src.MiddleName} {src.LastName}".Trim()))
			.ForMember(dest => dest.Standard, opt => opt.Ignore())
			.ForMember(dest => dest.AcademicMediumId, opt => opt.MapFrom(src => src.EducationMediumId))
			.ForMember(dest => dest.AcademicMediumName, opt => opt.MapFrom(src => src.EducationMedium != null ? src.EducationMedium.Name : null))
			.ForMember(dest => dest.AcademicBoardId, opt => opt.Ignore())
			.ForMember(dest => dest.AcademicBoardName, opt => opt.Ignore())
			.ForMember(dest => dest.AcademicYearId, opt => opt.MapFrom(src => src.AcademicYearId))
			.ForMember(dest => dest.AcademicYearName, opt => opt.Ignore())
			.ForMember(dest => dest.DateOfBirth, opt => opt.MapFrom(src => src.AdditionalDetail != null ? src.AdditionalDetail.DateOfBirthday : null))
			.ForMember(dest => dest.Age, opt => opt.MapFrom(src => src.AdditionalDetail != null ? src.AdditionalDetail.DateOfBirthday : null))
			.ForMember(dest => dest.DateOfInquiry, opt => opt.MapFrom(src => src.AdditionalDetail != null ? src.AdditionalDetail.DateOfInquiry : null))
			.ForMember(dest => dest.Gender, opt => opt.MapFrom(src => src.AdditionalDetail != null && src.AdditionalDetail.Gender.HasValue
				? src.AdditionalDetail.Gender.Value.ToString()
				: null))
			.ForMember(dest => dest.Address1, opt => opt.MapFrom(src => src.ContactDetail != null ? src.ContactDetail.Address1 : null))
			.ForMember(dest => dest.Address2, opt => opt.MapFrom(src => src.ContactDetail != null ? src.ContactDetail.Address2 : null))
			.ForMember(dest => dest.Address3, opt => opt.MapFrom(src => src.ContactDetail != null ? src.ContactDetail.Address3 : null))
			.ForMember(dest => dest.City, opt => opt.MapFrom(src => src.ContactDetail != null ? src.ContactDetail.City : null))
			.ForMember(dest => dest.State, opt => opt.MapFrom(src => src.ContactDetail != null && src.ContactDetail.State != null ? src.ContactDetail.State : null))
			.ForMember(dest => dest.Country, opt => opt.MapFrom(src => src.ContactDetail != null && src.ContactDetail.Country != null ? src.ContactDetail.Country : null))
			.ForMember(dest => dest.PinCode, opt => opt.MapFrom(src => src.ContactDetail != null ? src.ContactDetail.Pincode : null))
			.ForMember(dest => dest.Address, opt => opt.MapFrom(src =>
				src.ContactDetail == null ? null : string.Join(", ", new[]
				{
					src.ContactDetail.Address1,
					src.ContactDetail.Address2,
					src.ContactDetail.Address3,
					src.ContactDetail.City,
					src.ContactDetail.State != null ? src.ContactDetail.State.Name : null,
					src.ContactDetail.Country != null ? src.ContactDetail.Country.Name : null
				}.Where(x => !string.IsNullOrWhiteSpace(x)))))
			.ForMember(dest => dest.PrimaryContactNo, opt => opt.MapFrom(src => src.ContactDetail != null ? src.ContactDetail.PrimaryContactNo : null))
			.ForMember(dest => dest.SecondaryContactNo, opt => opt.MapFrom(src => src.ContactDetail != null ? src.ContactDetail.SecondaryContactNo : null))
			.ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.ContactDetail != null ? src.ContactDetail.Email : null))
			.ForMember(dest => dest.PreviousSchool, opt => opt.MapFrom(src => src.AdditionalDetail != null ? src.AdditionalDetail.PreviousSchool : null))
			.ForMember(dest => dest.InquiryPreference, opt => opt.MapFrom(src => src.InquiryPreference != null ? src.InquiryPreference.PreferenceName : null))
			.ForMember(dest => dest.NextReminderDate, opt => opt.Ignore())
			.IncludeAllDerived();

		// Map Student aggregate to StudentRegistrationFormDto (derived from StudentInquiryDto)
		CreateMap<Student, StudentRegistrationFormDto>()
			.IncludeBase<Student, StudentInquiryDto>()
			.ForMember(dest => dest.FormNo, opt => opt.MapFrom(src => src.AdditionalDetail != null ? src.AdditionalDetail.RegistrationFormNo ?? string.Empty : string.Empty));

		// Map Student aggregate to StudentRegistrationDto
		CreateMap<Student, StudentRegistrationDto>()
			.ForMember(dest => dest.StudentId, opt => opt.MapFrom(src => src.Id))
			.ForMember(dest => dest.InquiryNo, opt => opt.MapFrom(src => src.AdditionalDetail != null ? src.AdditionalDetail.InquiryNo : null))
			.ForMember(dest => dest.StudentName, opt => opt.MapFrom(src =>
				$"{src.FirstName} {src.MiddleName} {src.LastName}".Trim()))
			.ForMember(dest => dest.Standard, opt => opt.Ignore()) // Standard name not available on aggregate
			.ForMember(dest => dest.AcademicMediumId, opt => opt.MapFrom(src => src.EducationMediumId))
			.ForMember(dest => dest.AcademicMediumName, opt => opt.Ignore())
			.ForMember(dest => dest.AcademicBoardId, opt => opt.Ignore())
			.ForMember(dest => dest.AcademicBoardName, opt => opt.Ignore())
			.ForMember(dest => dest.AcademicYearId, opt => opt.MapFrom(src => src.AcademicYearId))
			.ForMember(dest => dest.AcademicYearName, opt => opt.Ignore())
			.ForMember(dest => dest.DateOfBirth, opt => opt.MapFrom(src => src.AdditionalDetail != null ? src.AdditionalDetail.DateOfBirthday : null))
			.ForMember(dest => dest.Age, opt => opt.MapFrom(src => src.AdditionalDetail != null ? src.AdditionalDetail.DateOfBirthday : null))
			.ForMember(dest => dest.DateOfInquiry, opt => opt.MapFrom(src => src.AdditionalDetail != null ? src.AdditionalDetail.DateOfInquiry : null))
			.ForMember(dest => dest.Gender, opt => opt.MapFrom(src => src.AdditionalDetail != null && src.AdditionalDetail.Gender.HasValue
				? src.AdditionalDetail.Gender.Value.ToString()
				: null))
			.ForMember(dest => dest.Address, opt => opt.MapFrom(src =>
				src.ContactDetail == null ? null : string.Join(", ", new[]
				{
					src.ContactDetail.Address1,
					src.ContactDetail.Address2,
					src.ContactDetail.Address3,
					src.ContactDetail.City,
					src.ContactDetail.State != null ? src.ContactDetail.State.Name : null,
					src.ContactDetail.Country != null ? src.ContactDetail.Country.Name : null
				}.Where(x => !string.IsNullOrWhiteSpace(x)))))
			.ForMember(dest => dest.PrimaryContactNo, opt => opt.MapFrom(src => src.ContactDetail != null ? src.ContactDetail.PrimaryContactNo : null))
			.ForMember(dest => dest.SecondaryContactNo, opt => opt.MapFrom(src => src.ContactDetail != null ? src.ContactDetail.SecondaryContactNo : null))
			.ForMember(dest => dest.PreviousSchool, opt => opt.MapFrom(src => src.AdditionalDetail != null ? src.AdditionalDetail.PreviousSchool : null))
			.ForMember(dest => dest.FormNo, opt => opt.MapFrom(src => src.AdditionalDetail != null ? src.AdditionalDetail.RegistrationFormNo ?? string.Empty : string.Empty))
			.ForMember(dest => dest.Progress, opt => opt.MapFrom(src => src.AdditionalDetail != null && src.AdditionalDetail.StudentRegistrationState != StudentRegistrationStateEnum.Default 
				? (double)(int)src.AdditionalDetail.StudentRegistrationState / (Enum.GetValues(typeof(StudentRegistrationStateEnum)).Length - 1) * 100 
				: 0));

		// Create a mapping from the anonymous type result to StudentInquiryDto
		CreateMap<StudentQueryResult, StudentInquiryDto>()
			.ForMember(dest => dest.StudentId, opt => opt.MapFrom(src => src.Student.Id))
			.ForMember(dest => dest.InquiryNo, opt => opt.MapFrom(src => src.AdditionalDetail.InquiryNo))
			.ForMember(dest => dest.StudentName, opt => opt.MapFrom(src =>
				$"{src.Student.FirstName} {src.Student.MiddleName} {src.Student.LastName}".Trim()))
			.ForMember(dest => dest.Standard, opt => opt.Ignore())
			.ForMember(dest => dest.AcademicMediumId, opt => opt.MapFrom(src => src.Student.EducationMediumId))
			.ForMember(dest => dest.AcademicMediumName, opt => opt.Ignore())
			.ForMember(dest => dest.AcademicBoardId, opt => opt.Ignore())
			.ForMember(dest => dest.AcademicBoardName, opt => opt.Ignore())
			.ForMember(dest => dest.AcademicYearId, opt => opt.MapFrom(src => src.Student.AcademicYearId))
			.ForMember(dest => dest.AcademicYearName, opt => opt.Ignore())
			.ForMember(dest => dest.DateOfBirth, opt => opt.MapFrom(src => src.AdditionalDetail.DateOfBirthday))
			.ForMember(dest => dest.Age, opt => opt.MapFrom(src => src.AdditionalDetail.DateOfBirthday))
			.ForMember(dest => dest.DateOfInquiry, opt => opt.MapFrom(src => src.AdditionalDetail.DateOfInquiry))
			.ForMember(dest => dest.Gender, opt => opt.MapFrom(src => src.AdditionalDetail.Gender.ToString()))
			.ForMember(dest => dest.Address, opt => opt.MapFrom(src =>
				string.Join(", ", new[] {
					src.ContactDetail.Address1,
					src.ContactDetail.Address2,
					src.ContactDetail.Address3,
					src.ContactDetail.City,
					src.ContactDetail.State != null ? src.ContactDetail.State.Name : null,
					src.ContactDetail.Country != null ? src.ContactDetail.Country.Name : null
				}.Where(x => !string.IsNullOrWhiteSpace(x)))))
			.ForMember(dest => dest.PrimaryContactNo, opt => opt.MapFrom(src => src.ContactDetail.PrimaryContactNo))
			.ForMember(dest => dest.SecondaryContactNo, opt => opt.MapFrom(src => src.ContactDetail.SecondaryContactNo));

		// Add converter for Age calculation
		CreateMap<DateTimeOffset?, string>().ConvertUsing<AgeConverter>();

		CreateMap<StudentQueryResult, StudentRegistrationFormDto>()
			.ForMember(dest => dest.StudentId, opt => opt.MapFrom(src => src.Student.Id))
			.ForMember(dest => dest.InquiryNo, opt => opt.MapFrom(src => src.AdditionalDetail.InquiryNo))
			.ForMember(dest => dest.StudentName, opt => opt.MapFrom(src =>
				$"{src.Student.FirstName} {src.Student.MiddleName} {src.Student.LastName}".Trim()))
			.ForMember(dest => dest.Standard, opt => opt.MapFrom(src => "Standard Name")) // Fix this hardcoded value
			.ForMember(dest => dest.DateOfBirth, opt => opt.MapFrom(src => src.AdditionalDetail.DateOfBirthday))
			.ForMember(dest => dest.Age, opt => opt.MapFrom(src => src.AdditionalDetail.DateOfBirthday))
			.ForMember(dest => dest.DateOfInquiry, opt => opt.MapFrom(src => src.AdditionalDetail.DateOfInquiry))
			.ForMember(dest => dest.Gender, opt => opt.MapFrom(src => src.AdditionalDetail.Gender.ToString()))
			.ForMember(dest => dest.Address, opt => opt.MapFrom(src =>
				string.Join(", ", new[] {
					src.ContactDetail.Address1,
					src.ContactDetail.Address2,
					src.ContactDetail.Address3,
					src.ContactDetail.City,
					src.ContactDetail.State != null ? src.ContactDetail.State.Name : null,
					src.ContactDetail.Country != null ? src.ContactDetail.Country.Name : null
				}.Where(x => !string.IsNullOrWhiteSpace(x)))))
			.ForMember(dest => dest.PrimaryContactNo, opt => opt.MapFrom(src => src.ContactDetail.PrimaryContactNo))
			.ForMember(dest => dest.SecondaryContactNo, opt => opt.MapFrom(src => src.ContactDetail.SecondaryContactNo))
			.ForMember(dest => dest.FormNo, opt => opt.MapFrom(src => src.AdditionalDetail.RegistrationFormNo));

		CreateMap<StudentQueryResult, StudentRegistrationDto>()
			.ForMember(dest => dest.StudentId, opt => opt.MapFrom(src => src.Student.Id))
			.ForMember(dest => dest.InquiryNo, opt => opt.MapFrom(src => src.AdditionalDetail.InquiryNo))
			.ForMember(dest => dest.StudentName, opt => opt.MapFrom(src =>
				$"{src.Student.FirstName} {src.Student.MiddleName} {src.Student.LastName}".Trim()))
			.ForMember(dest => dest.Standard, opt => opt.MapFrom(src => "Standard Name")) // Fix this hardcoded value
			.ForMember(dest => dest.DateOfBirth, opt => opt.MapFrom(src => src.AdditionalDetail.DateOfBirthday))
			.ForMember(dest => dest.Age, opt => opt.MapFrom(src => src.AdditionalDetail.DateOfBirthday))
			.ForMember(dest => dest.DateOfInquiry, opt => opt.MapFrom(src => src.AdditionalDetail.DateOfInquiry))
			.ForMember(dest => dest.Gender, opt => opt.MapFrom(src => src.AdditionalDetail.Gender.ToString()))
			.ForMember(dest => dest.Address, opt => opt.MapFrom(src =>
				string.Join(", ", new[] {
					src.ContactDetail.Address1,
					src.ContactDetail.Address2,
					src.ContactDetail.Address3,
					src.ContactDetail.City,
					src.ContactDetail.State != null ? src.ContactDetail.State.Name : null,
					src.ContactDetail.Country != null ? src.ContactDetail.Country.Name : null
				}.Where(x => !string.IsNullOrWhiteSpace(x)))))
			.ForMember(dest => dest.PrimaryContactNo, opt => opt.MapFrom(src => src.ContactDetail.PrimaryContactNo))
			.ForMember(dest => dest.SecondaryContactNo, opt => opt.MapFrom(src => src.ContactDetail.SecondaryContactNo))
			.ForMember(dest => dest.FormNo, opt => opt.MapFrom(src => src.AdditionalDetail.RegistrationFormNo))
			.ForMember(dest => dest.Progress, opt => opt.MapFrom(src => src.AdditionalDetail.StudentRegistrationState == StudentRegistrationStateEnum.Default ? 0 :
	(double) (int) src.AdditionalDetail.StudentRegistrationState / (Enum.GetValues(typeof(StudentRegistrationStateEnum)).Length - 1) * 100));

		// create a mapping from StudentQueryResult to StudentGeneralInformationDto
		CreateMap<StudentQueryResult, StudentGeneralInformationDto>()
			.ForMember(dest => dest.StudentId, opt => opt.MapFrom(src => src.Student.Id))
			.ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.Student.FirstName))
			.ForMember(dest => dest.MiddleName, opt => opt.MapFrom(src => src.Student.MiddleName))
			.ForMember(dest => dest.LastName, opt => opt.MapFrom(src => src.Student.LastName))
			.ForMember(dest => dest.DateOfBirth, opt => opt.MapFrom(src => src.AdditionalDetail.DateOfBirthday))
			.ForMember(dest => dest.Gender, opt => opt.MapFrom(src => src.AdditionalDetail.Gender))
			.ForMember(dest => dest.ProfilePictureUrl, opt => opt.MapFrom(src => src.AdditionalDetail.ProfilePictureUrl))
			.ForMember(dest => dest.PrimaryContactNo, opt => opt.MapFrom(src => src.ContactDetail.PrimaryContactNo))
			.ForMember(dest => dest.SecondaryContactNo, opt => opt.MapFrom(src => src.ContactDetail.SecondaryContactNo))
			.ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.ContactDetail.Email))
			.ForMember(dest => dest.AadharNo, opt => opt.MapFrom(src => src.AdditionalDetail.AdharNo))
			.ForMember(dest => dest.ApparId, opt => opt.MapFrom(src => src.AdditionalDetail.ApparId))
			.ForMember(dest => dest.PanNo, opt => opt.MapFrom(src => src.AdditionalDetail.PanNo))
			.ForMember(dest => dest.InquiryPreferenceId, opt => opt.MapFrom(src => src.AdditionalDetail.InquiryPreferenceId))
			.ForMember(dest => dest.Religion, opt => opt.MapFrom(src => src.AdditionalDetail.Religion))
			.ForMember(dest => dest.FamilyCast, opt => opt.MapFrom(src => src.AdditionalDetail.FamilyCast))
			.ForMember(dest => dest.CastCategory, opt => opt.MapFrom(src => src.AdditionalDetail.CastCategory))
			.ForMember(dest => dest.Nationality, opt => opt.MapFrom(src => src.AdditionalDetail.Nationality))
			.ForMember(dest => dest.BirthPlace, opt => opt.MapFrom(src => src.AdditionalDetail.BirthPlace))
			.ForMember(dest => dest.MotherTongue, opt => opt.MapFrom(src => src.AdditionalDetail.MotherTongue))
			.ForMember(dest => dest.Address1, opt => opt.MapFrom(src => src.ContactDetail.Address1))
			.ForMember(dest => dest.Address2, opt => opt.MapFrom(src => src.ContactDetail.Address2))
			.ForMember(dest => dest.Address3, opt => opt.MapFrom(src => src.ContactDetail.Address3))
			.ForMember(dest => dest.City, opt => opt.MapFrom(src => src.ContactDetail.City))
			.ForMember(dest => dest.State, opt => opt.MapFrom(src => src.ContactDetail.State))
			.ForMember(dest => dest.Country, opt => opt.MapFrom(src => src.ContactDetail.Country))
			.ForMember(dest => dest.Pincode, opt => opt.MapFrom(src => src.ContactDetail.Pincode))
			.ForMember(dest => dest.PreviousSchool, opt => opt.MapFrom(src => src.AdditionalDetail.PreviousSchool))
			.ForMember(dest => dest.LastClassPassed, opt => opt.MapFrom(src => src.AdditionalDetail.LastClassPassed));
	}
}
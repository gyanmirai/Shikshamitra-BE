using AutoMapper;
using ShikshaMitra.Common;
using ShikshaMitra.Students.StudentDetails;
using ShikshaMitra.Students.StudentEnrollments;
using System;
using System.Linq;

namespace ShikshaMitra.Students;

public class StudentMapperProfile : Profile
{
	public StudentMapperProfile()
	{
		CreateMap<Student, StudentInquiryDto>();

		// Create a mapping from the anonymous type result to StudentInquiryDto
		CreateMap<StudentQueryResult, StudentInquiryDto>()
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
					src.ContactDetail.State,
					src.ContactDetail.Country
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
					src.ContactDetail.State,
					src.ContactDetail.Country
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
					src.ContactDetail.State,
					src.ContactDetail.Country
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
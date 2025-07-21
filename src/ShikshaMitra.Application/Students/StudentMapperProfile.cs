using AutoMapper;
using ShikshaMitra.Common;
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
	}
}
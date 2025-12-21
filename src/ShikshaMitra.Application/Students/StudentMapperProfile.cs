using AutoMapper;
using ShikshaMitra.Common;
using ShikshaMitra.Students.Attendance.Settings;
using ShikshaMitra.Students.Attendances.Settings;
using ShikshaMitra.Students.Enrollments.Registrations;
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
		CreateMap<Student, StudentEnrollmentListDto>()
			.ForMember(dest => dest.StudentId, opt => opt.MapFrom(src => src.Id))
			.ForMember(dest => dest.InquiryNo, opt => opt.MapFrom(src => src.AdditionalDetail != null ? src.AdditionalDetail.InquiryNo : null))
			.ForMember(dest => dest.StudentName, opt => opt.MapFrom(src =>
				$"{src.FirstName} {src.MiddleName} {src.LastName}".Trim()))
			.ForMember(dest => dest.Standard, opt => opt.Ignore())
			.ForMember(dest => dest.AcademicMediumId, opt => opt.MapFrom(src => src.AcademicMediumId))
			.ForMember(dest => dest.AcademicMediumName, opt => opt.MapFrom(src => src.AcademicMedium != null ? src.AcademicMedium.Name : null))
			.ForMember(dest => dest.AcademicBoardId, opt => opt.Ignore())
			.ForMember(dest => dest.AcademicBoardName, opt => opt.Ignore())
			.ForMember(dest => dest.AcademicYearId, opt => opt.MapFrom(src => src.AcademicYearId))
			.ForMember(dest => dest.AcademicYearName, opt => opt.Ignore())
			.ForMember(dest => dest.DateOfBirth, opt => opt.MapFrom(src => src.AdditionalDetail != null ? src.AdditionalDetail.DateOfBirth : null))
			.ForMember(dest => dest.Age, opt => opt.MapFrom(src => src.AdditionalDetail != null ? src.AdditionalDetail.DateOfBirth : null))
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
			.ForMember(dest => dest.FormNo, opt => opt.MapFrom(src => src.AdditionalDetail != null ? src.AdditionalDetail.FormNo : null))
			.IncludeAllDerived();

		// Add converter for Age calculation
		CreateMap<DateTimeOffset?, string>().ConvertUsing<AgeConverter>();

               CreateMap<Student, StudentGeneralDetailsDto>();

               // StudentNote mappings
               CreateMap<StudentNote, ShikshaMitra.Students.Enrollments.Notes.StudentNoteDto>();
               CreateMap<ShikshaMitra.Students.Enrollments.Notes.CreateUpdateStudentNoteDto, StudentNote>();
       }
}
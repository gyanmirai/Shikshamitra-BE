using AutoMapper;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Application.Contracts.Groups;
using ShikshaMitra.Application.Contracts.Communication.AnnouncementTypes;
using ShikshaMitra.Communication.AnnouncementTypes;
using ShikshaMitra.Common;
using ShikshaMitra.Groups;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Calendar;
using ShikshaMitra.HR.Calendar.EventTypes;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Calendar.Events;
using ShikshaMitra.Application.Contracts.HR.Calendar.Holidays;
using ShikshaMitra.Application.Contracts.HR.Calendar.Events;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.HR.Employees.ReportingManager;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings;
using ShikshaMitra.TenantSettings.RoomsManagement.Buildings;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Finance.StudentFeeAssignment;
using ShikshaMitra.Application.Contracts.Finance.StudentFeeAssignment.Dtos;

namespace ShikshaMitra;

public class ShikshaMitraApplicationAutoMapperProfile : Profile
{
	public ShikshaMitraApplicationAutoMapperProfile()
	{
		/* You can configure your AutoMapper mapping configuration here.
		 * Alternatively, you can split your mapping configurations
		 * into multiple profile classes for a better organization. */

		// BookCategory and LibraryTiming mappings are defined at the bottom of this profile

		// Event Types mapping
		CreateMap<EventType, EventTypeDto>();
		CreateMap<CreateUpdateEventTypeDto, EventType>();

		// Holiday Type mapping
		CreateMap<HolidayType, HolidayTypeDto>();
		CreateMap<CreateUpdateHolidayTypeDto, HolidayType>();

		// Holiday mapping with nested HolidayType
		CreateMap<Holiday, HolidayDto>()
			.ForMember(dest => dest.HolidayType,
				opt => opt.Ignore()); // Will be populated separately
		CreateMap<CreateUpdateHolidayDto, Holiday>();

		// Event mapping with nested EventType
		CreateMap<ShikshaMitra.HR.Calendar.Events.Event, EventDto>()
			.ForMember(dest => dest.EventType,
				opt => opt.Ignore()); // Will be populated separately
		CreateMap<CreateUpdateEventDto, ShikshaMitra.HR.Calendar.Events.Event>();

		// Earning Components
		CreateMap<EarningComponent, EarningComponentDto>();

		// Deduction Components
		CreateMap<DeductionComponent, DeductionComponentDto>();

		// Building and Floor mappings
		CreateMap<Building, BuildingDto>();
		CreateMap<Floor, FloorDto>();

		// Room mappings
		CreateMap<Room, RoomDto>();
		CreateMap<CreateUpdateRoomDto, Room>();

		// Subject mappings - Curriculum Management
		CreateMap<Subject, SubjectDto>();
		CreateMap<CreateUpdateSubjectDto, Subject>().ForMember(dest => dest.Id, opt => opt.Ignore());

		// Document Types - Manual mapping used due to enum list conversion

		// Employee Leave Management
		CreateMap<LeaveType, LeaveTypeDto>();

		// Leave Policy mapping with custom PaidType conversion
		CreateMap<LeavePolicy, LeavePolicyDto>()
			.ForMember(dest => dest.PaidType,
				opt => opt.MapFrom(src => src.PaidType == LeavePaidType.Paid ? "Paid" : "Unpaid"));

		// Employee Reporting Manager mappings
		CreateMap<Employee, EmployeeBasicDto>()
			.ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.EmployeeName));

		// Employee Personal Info mappings
		CreateMap<Employee, EmployeePersonalInfoDto>()
			.ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.EmployeeName))
			.ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.FirstName ?? string.Empty))
			.ForMember(dest => dest.LastName, opt => opt.MapFrom(src => src.LastName ?? string.Empty))
			.ForMember(dest => dest.MobileNo, opt => opt.MapFrom(src => src.MobileNo ?? string.Empty))
			.ForMember(dest => dest.FamilyCaste, opt => opt.MapFrom(src => src.FamilyCast))
			.ForMember(dest => dest.Pincode, opt => opt.MapFrom(src => src.PinCode))
			.ForMember(dest => dest.RoleName, opt => opt.MapFrom(src => src.Role != null ? src.Role.Name : null))
			.ForMember(dest => dest.EmployeeStatusType, opt => opt.MapFrom(src => src.EmployeeStatusType ?? EmployeeStatusType.Active));

		// Group mappings - Note: GroupDto is manually mapped in GroupAppService.MapToDto()
		// These mappings are kept for basic property mapping but nested objects are handled manually
		CreateMap<Group, GroupDto>()
			.ForMember(dest => dest.MemberCount, opt => opt.Ignore()) // Populated separately
			.ForMember(dest => dest.RuleBased, opt => opt.Ignore()) // Populated manually in MapToDto
			.ForMember(dest => dest.NameBased, opt => opt.Ignore()); // Populated manually in MapToDto

		CreateMap<GroupStudentRule, StudentRuleDto>();
		CreateMap<GroupEmployeeRule, EmployeeRuleDto>();
		CreateMap<GroupMember, GroupMemberDto>()
			.ForMember(dest => dest.DisplayName, opt => opt.Ignore()); // Populated separately

		// AnnouncementType mappings
		CreateMap<AnnouncementType, AnnouncementTypeDto>()
			.ForMember(dest => dest.AnnouncementType, opt => opt.MapFrom(src => src.AnnouncementTypeName));
		CreateMap<CreateUpdateAnnouncementTypeDto, AnnouncementType>()
			.ForMember(dest => dest.AnnouncementTypeName, opt => opt.MapFrom(src => src.AnnouncementType));

		// Student Fee Structure mappings
		CreateMap<StudentFeeStructure, StudentFeeStructureResponseDto>()
			.ForMember(dest => dest.StudentFeeStructureId, opt => opt.MapFrom(src => src.Id))
			.ForMember(dest => dest.FeeHeads, opt => opt.MapFrom(src => src.StudentFeeHeads));
		CreateMap<StudentFeeHead, StudentFeeHeadDto>();

		// Library - BookCategory mappings
		CreateMap<ShikshaMitra.Library.Setting.GeneralSetting.BookCategory.BookCategory,
			ShikshaMitra.Library.Setting.GeneralSetting.BookCategory.BookCategoryDto>()
			.ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Name));

		// Library - LibraryTiming mappings
		CreateMap<ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings.LibraryTiming,
			ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings.LibraryTimingDto>();

		// Library - LibraryChargeCategory mappings
		CreateMap<ShikshaMitra.Library.Setting.ChargeCategory.LibraryChargeCategory,
			ShikshaMitra.Library.Setting.ChargeCategory.Dtos.LibraryChargeCategoryDto>()
			.ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.ChargeType));
	}
}
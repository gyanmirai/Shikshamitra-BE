using AutoMapper;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Common;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.HR.Employees.ReportingManager;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings;
using ShikshaMitra.TenantSettings.RoomsManagement.Buildings;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.Academic.Curriculum.Settings;

namespace ShikshaMitra;

public class ShikshaMitraApplicationAutoMapperProfile : Profile
{
	public ShikshaMitraApplicationAutoMapperProfile()
	{
		/* You can configure your AutoMapper mapping configuration here.
		 * Alternatively, you can split your mapping configurations
		 * into multiple profile classes for a better organization. */

		CreateMap<BookCategory, BookCategoryDto>();
		CreateMap<CreateUpdateBookCategoryDto, BookCategory>();

		CreateMap<LibraryTiming, LibraryTimingDto>();
		CreateMap<LibraryTimingDto, LibraryTiming>();

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
	}
}
using AutoMapper;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings;

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

		// Document Types - Manual mapping used due to enum list conversion

		// Employee Leave Management
		CreateMap<LeaveType, LeaveTypeDto>();
		// LeavePolicy uses custom mapping in service (MapToDto method)
	}
}
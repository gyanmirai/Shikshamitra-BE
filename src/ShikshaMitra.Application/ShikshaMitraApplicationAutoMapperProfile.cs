using AutoMapper;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings;
using ShikshaMitra.TenantSettings.RoomsManagement.Buildings;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;

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
	}
}
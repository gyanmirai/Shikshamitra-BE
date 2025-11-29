using AutoMapper;
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

		// Document Types - Manual mapping used due to enum list conversion
	}
}
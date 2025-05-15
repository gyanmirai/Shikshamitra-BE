using AutoMapper;
using ShikshaMitra.Authors;
using ShikshaMitra.Books;
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


        CreateMap<Book, BookDto>();
        CreateMap<CreateUpdateBookDto, Book>();

        CreateMap<Author, AuthorDto>();
        CreateMap<Author, AuthorLookupDto>();

		CreateMap<BookCategory, BookCategoryDto>();
		CreateMap<CreateUpdateBookCategoryDto, BookCategory>();

		CreateMap<LibraryTiming, LibraryTimingDto>();
		CreateMap<LibraryTimingDto, LibraryTiming>();

	}
}
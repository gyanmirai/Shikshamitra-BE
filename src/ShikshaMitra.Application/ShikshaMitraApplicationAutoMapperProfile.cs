using AutoMapper;
using ShikshaMitra.Authors;
using ShikshaMitra.Books;
using ShikshaMitra.Onboarding.Divisions;
using ShikshaMitra.Onboarding.EducationalMediums;
using ShikshaMitra.Onboarding.EducationBoards;
using ShikshaMitra.Onboarding.Standards;

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
    }
}
using AutoMapper;
using ShikshaMitra.Library.BookManagement.AddNewBook;
using ShikshaMitra.Library.BookManagement.AddNewBooks;
using ShikshaMitra.Library.BookManagement.BookOverviews;
using ShikshaMitra.Library.BookManagement.IssueBook;
using ShikshaMitra.Library.BookManagement.IssueBooks;
using ShikshaMitra.Library.BookManagement.ReturnBook;
using ShikshaMitra.Library.BookManagement.ReturnBooks;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings;
using ShikshaMitra.Library.Setting.GeneralSetting.LoanPolicies;
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

		CreateMap<LoanPolicy, LoanPolicyDto>();
		CreateMap<CreateUpdateLoanPolicyDto, LoanPolicy>();

		CreateMap<AddNewBook, AddNewBookDto>();
		CreateMap<CreateUpdateAddNewBookDto, AddNewBook>();

		CreateMap<IssueBook, IssueBookDto>();
		CreateMap<CreateUpdateIssueBookDto, IssueBook>();

		CreateMap<ReturnBook, ReturnBookDto>();
		CreateMap<CreateUpdateReturnBookDto, ReturnBook>();

		CreateMap<BookOverview, BookOverviewDto>();
		CreateMap<CreateUpdateBookOverviewDto, BookOverview>();
    }
}
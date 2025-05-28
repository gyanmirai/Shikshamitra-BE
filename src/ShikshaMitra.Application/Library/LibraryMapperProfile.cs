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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Library
{
	internal class LibraryMapperProfile : Profile
	{
		public LibraryMapperProfile()
		{
			#region Setting
			#region General Setting 
			CreateMap<BookCategory, BookCategoryDto>();
			CreateMap<CreateUpdateBookCategoryDto, BookCategory>();

			CreateMap<LibraryTiming, LibraryTimingDto>();
			CreateMap<LibraryTimingDto, LibraryTiming>();

			CreateMap<LoanPolicy, LoanPolicyDto>();
			CreateMap<CreateUpdateLoanPolicyDto, LoanPolicy>();
			#endregion

			#region Book Management  
			CreateMap<AddNewBook, AddNewBookDto>();
			CreateMap<AddNewBookDto, AddNewBook>();

			CreateMap<IssueBook, IssueBookDto>();
			CreateMap<IssueBookDto, IssueBook>();

			CreateMap<ReturnBook, ReturnBookDto>();
			CreateMap<ReturnBookDto, ReturnBook>();

			CreateMap<BookOverview, BookOverviewDto>();
			CreateMap<BookOverviewDto, BookOverview>();
            #endregion
		}
	}
}

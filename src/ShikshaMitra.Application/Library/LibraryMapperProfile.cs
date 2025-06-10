using AutoMapper;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.BooksAssignments;
using ShikshaMitra.Library.BooksReservations;
using ShikshaMitra.Library.Fines;
using ShikshaMitra.Library.Members;
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
            #endregion

            #region Books Management
            CreateMap<Book, BookDto>();
            CreateMap<BookDto, Book>();
            CreateMap<AddNewBookDto, Book>();
            CreateMap<Book, BookOverviewDto>();
			#endregion

			#region Books Assignments 
			CreateMap<BooksAssignment, BooksAssignmentDto>();
			CreateMap<BooksAssignmentDto, BooksAssignment>();

			CreateMap<IssueBookDto, BooksAssignment>();

            CreateMap<ReturnBookDto, BooksAssignment>();
            #endregion

            //#region Book Reservations
            //CreateMap<BookReservation, CreateBookReservationDto>();
            //CreateMap<CreateBookReservationDto, BookReservation>();

            //#endregion
            //#region Members
            //CreateMap<Member, MemberDto>();
            //CreateMap<MemberDto, Member>();

            //CreateMap<Member, MemberSummaryDto>();
            //CreateMap<MemberSummaryDto, Member>();
            //#endregion

            //#region Fines
            //CreateMap<Fine, FineDto>();
            //CreateMap<FineDto, Fine>();

            //CreateMap<CreateUpdateFineDto, Fine>();
            //CreateMap<Fine, CreateUpdateFineDto>();
            //#endregion
        }
    }
}

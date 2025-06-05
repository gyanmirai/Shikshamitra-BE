using AutoMapper;
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
        }
    }
}

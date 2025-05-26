using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.BookManagement.BookOverviews
{
	public interface IBookOverviewAppService :
		ICrudAppService<
			BookOverviewDto,
			Guid,
			PagedAndSortedResultRequestDto,
			CreateUpdateBookOverviewDto>
	{
    }
}

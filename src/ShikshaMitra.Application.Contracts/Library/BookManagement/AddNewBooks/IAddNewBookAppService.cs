using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.BookManagement.AddNewBook;

public interface IAddNewBookAppService : ICrudAppService<
	 AddNewBookDto,
	 Guid,
	 PagedAndSortedResultRequestDto,
	 CreateUpdateAddNewBookDto>
{
}

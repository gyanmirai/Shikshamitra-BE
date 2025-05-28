using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.BookManagement.IssueBook;

public interface IIssueBookAppService : ICrudAppService<
	IssueBookDto,
	Guid,
	PagedAndSortedResultRequestDto,
	CreateUpdateIssueBookDto>
{

}

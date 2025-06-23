using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShikshaMitra.Library.BooksAssignments;

public interface IBooksAssignmentAppService
{
	Task<BooksAssignmentDto> CreateAsync(CreateUpdateBooksAssignmentDto input);
	Task<List<BooksAssignmentDto>> GetListAsync();
    Task<BooksAssignmentDto> GetAsync(Guid id);
	Task<BooksAssignmentDto> UpdateAsync(Guid id, CreateUpdateBooksAssignmentDto input);
    Task<string> IssueBookAsync(IssueBookDto input);
    Task<string> ReturnBookAsync(ReturnBookDto input);
}

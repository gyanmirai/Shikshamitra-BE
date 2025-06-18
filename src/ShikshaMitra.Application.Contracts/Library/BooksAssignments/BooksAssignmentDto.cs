using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Library.BooksAssignments;

public class BooksAssignmentDto : EntityDto<Guid>
{
	public Guid Id { get; set; }
	public Guid? TenantId { get; set; }

	public Guid BookId { get; set; }
	public Guid MemberId { get; set; }
    public string BookTitle { get; set; }
	public DateTime IssueDate { get; set; }
	public DateTime DueDate { get; set; }
	public DateTime? ReturnDate { get; set; }
}

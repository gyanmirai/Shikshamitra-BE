using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.BookManagement.BookOverviews;

public class BookOverview : AuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public string ISBN { get; set; }
	public string Name { get; set; }
	public int Borrowed { get; set; }
	public int Overdue { get; set; }
	public int Reserved { get; set; }

	private BookOverview() { }

	public BookOverview(Guid id, string isbn, string name, int borrowed, int overdue, int reserved, Guid? tenantId)
		: base(id)
	{
		ISBN = isbn;
		Name = name;
		Borrowed = borrowed;
		Overdue = overdue;
		Reserved = reserved;
		TenantId = tenantId;
	}
}


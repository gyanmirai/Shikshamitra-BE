using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.BookManagement.IssueBooks;

public class IssueBook : AuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
        public string Standard { get; set; }
	public string Division { get; set; }
	public string GrNumber { get; set; }
	public string StudentName { get; set; }

	public Guid BookId { get; set; }
	public string BookTitle { get; set; }
	public int EditionYear { get; set; }
	public string Publisher { get; set; }

	public DateTime IssueDate { get; set; }
	public DateTime DueDate { get; set; }

	// Member Information - (No pending charges for the selected member)
	public bool HasPendingCharges { get; set; }

	private IssueBook() { }

	public IssueBook(
		Guid id,
		string standard,
		string division,
		string grNumber,
		string studentName,
		Guid bookId,
		string bookTitle,
		int editionYear,
		string publisher,
		DateTime issueDate,
		DateTime dueDate,
		bool hasPendingCharges,
		Guid? tenantId)
		: base(id)
	{
		Standard = standard;
		Division = division;
		GrNumber = grNumber;
		StudentName = studentName;
		BookId = bookId;
		BookTitle = bookTitle;
		EditionYear = editionYear;
		Publisher = publisher;
		IssueDate = issueDate;
		DueDate = dueDate;
		HasPendingCharges = hasPendingCharges;
		TenantId = tenantId;
	}
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;

namespace ShikshaMitra.Library.Books;

public class LibraryCopyEventRecorder(IRepository<LibraryCopyEvent, Guid> events, IRepository<BookIssue, Guid> loans,
	ILibraryInventoryLock inventoryLock, ICurrentTenant tenant, IClock clock, IAsyncQueryableExecuter queries) : DomainService
{
	public Task RecordAsync(LibraryCopySnapshot before, BookCopy after, LibraryCopyEventType type,
		Guid actorId, string reason, Guid? relatedLoanId = null) =>
		RecordAsync(before, after, type, LibraryOperationActor.User(actorId), reason, relatedLoanId);

	public Task RecordAsync(LibraryCopySnapshot before, BookCopy after, LibraryCopyEventType type,
		LibraryOperationActor actor, string reason, Guid? relatedLoanId = null) =>
		RecordWithResultAsync(before, after, type, actor, reason, relatedLoanId);

	public async Task<LibraryCopyEvent> RecordWithResultAsync(LibraryCopySnapshot before, BookCopy after, LibraryCopyEventType type,
		LibraryOperationActor actor, string reason, Guid? relatedLoanId = null)
	{
		await inventoryLock.AcquireAsync(tenant.Id);
		if (after.TenantId != tenant.Id || before.TenantId != tenant.Id)
			throw new BusinessException(LibraryErrorCodes.CopyEventInvalid);
		if (relatedLoanId.HasValue)
		{
			var loan = await loans.FindAsync(relatedLoanId.Value);
			if (loan == null || loan.TenantId != tenant.Id)
				throw new BusinessException(LibraryErrorCodes.CopyEventInvalid, "The referenced loan does not belong to this copy.");
			if (loan.BookCopyId != after.Id)
				throw new BusinessException(LibraryErrorCodes.CopyLoanReconciliationRequired,
					"Review and reconcile the loan's physical copy reference before recording a copy event. An accession match alone does not confirm the copy.");
		}
		var sequence = checked(await LastSequenceAsync(after.Id) + 1);
		return await events.InsertAsync(new LibraryCopyEvent(GuidGenerator.Create(), sequence, type, before,
			LibraryCopySnapshot.Capture(after), actor, reason, new DateTimeOffset(clock.Now.ToUniversalTime()), relatedLoanId), autoSave: true);
	}

	public async Task<string> RevisionAsync(BookCopy copy, Guid? activeLoanId)
	{
		var data = JsonSerializer.Serialize(new
		{
			copy.Id, copy.BookId, copy.TenantId, copy.AccessionNumber, copy.Status, copy.SectionId, copy.ShelfId, copy.Remarks,
			ActiveLoanId = activeLoanId, Sequence = await LastSequenceAsync(copy.Id)
		});
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data)));
	}

	public Task<bool> HasEventsAsync(IReadOnlyCollection<Guid> copyIds) => events.AnyAsync(row => copyIds.Contains(row.CopyId));

	private async Task<long> LastSequenceAsync(Guid copyId) => await queries.FirstOrDefaultAsync(
		(await events.GetQueryableAsync()).Where(row => row.CopyId == copyId).OrderByDescending(row => row.Sequence).Select(row => row.Sequence));
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Receiving;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Library.InventoryVerification;

public sealed class LibraryStockReceiptCutoffReader(IRepository<LibraryReceivedCopy, Guid> receivedCopies,
	IRepository<LibraryStockReceiptLine, Guid> lines, IRepository<LibraryStockReceipt, Guid> receipts,
	IAsyncQueryableExecuter queries) : ITransientDependency
{
	internal async Task<HashSet<Guid>> FindAsync(Guid tenantId, DateTimeOffset openedAt, IQueryable<LibraryStockVerificationObservation> scans,
		IQueryable<Guid> allowedSectionIds)
	{
		var links = (await receivedCopies.GetQueryableAsync()).Where(row => row.TenantId == tenantId);
		var eligibleLines = (await lines.GetQueryableAsync()).Where(row => row.TenantId == tenantId && allowedSectionIds.Contains(row.SectionId));
		var accepted = (await receipts.GetQueryableAsync()).Where(row => row.TenantId == tenantId);
		var joined = from scan in scans
			join link in links on scan.CopyId equals (Guid?)link.CopyId
			join line in eligibleLines on link.ReceiptLineId equals line.Id
			join receipt in accepted on line.ReceiptId equals receipt.Id
			select new { scan.Id, scan.ObservedAt, receipt.RecordedAt };
		var evidence = await queries.ToListAsync(joined);
		return evidence.Where(row => row.RecordedAt > openedAt && row.RecordedAt <= row.ObservedAt).Select(row => row.Id).ToHashSet();
	}
}

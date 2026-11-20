using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Reservations;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.Uow;
using StockActions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Library.InventoryVerification;

namespace ShikshaMitra.Library.InventoryVerification;

public partial class LibraryStockVerificationAppService
{
	[UnitOfWork(true)]
	public async Task<PaginatedResponse<LibraryStockReconciliationRowDto>> GetReconciliationAsync(Guid sessionId,
		LibraryStockReconciliationSource source = LibraryStockReconciliationSource.Expected, int page = 1, int pageSize = 20)
	{
		var scope = await RequireScopeAsync(StockActions.View);
		if (!Enum.IsDefined(source)) throw Invalid();
		await inventoryLock.AcquireAsync(scope.TenantId);
		var session = await RequireSessionAsync(scope, sessionId);
		if (session.Status is not (LibraryStockVerificationStatus.Counting or LibraryStockVerificationStatus.InReview))
			throw new BusinessException(LibraryErrorCodes.StockVerificationInvalidState);
		if (session.Status == LibraryStockVerificationStatus.InReview) scope = ReviewScope(scope, session);

		var result = await ReadReconciliationRowsAsync(scope, session, source, page, pageSize);
		if (session.Status == LibraryStockVerificationStatus.InReview && result.Items.Count > 0)
			await AttachDecisionsAsync(scope.TenantId, sessionId, source, result.Items);
		return result;
	}

	private async Task<PaginatedResponse<LibraryStockReconciliationRowDto>> ReadReconciliationRowsAsync(StockScope scope,
		LibraryStockVerificationSession session, LibraryStockReconciliationSource source, int page, int pageSize, Guid? rowId = null,
		int maximumPageSize = 100)
	{
		var expected = (await baseline.GetQueryableAsync()).Where(row => row.TenantId == scope.TenantId && row.SessionId == session.Id);
		var scanned = (await observations.GetQueryableAsync()).Where(row => row.TenantId == scope.TenantId && row.SessionId == session.Id);
		if (source == LibraryStockReconciliationSource.Expected)
			return await ReadExpectedReconciliationAsync(scope, session, rowId.HasValue ? expected.Where(row => row.CopyId == rowId.Value) : expected,
				scanned, page, pageSize, maximumPageSize);

		var rows = source == LibraryStockReconciliationSource.UnknownScans
			? scanned.Where(row => !row.CopyId.HasValue)
			: LatestByCopy(scanned.Where(row => row.CopyId.HasValue && !expected.Any(copy => copy.CopyId == row.CopyId.Value)));
		var finding = source == LibraryStockReconciliationSource.UnknownScans ? LibraryStockFinding.UnknownAccession : LibraryStockFinding.OutsideBaseline;
		if (rowId.HasValue) rows = rows.Where(row => row.Id == rowId.Value);
		var result = await PageAsync(rows.OrderByDescending(row => row.Sequence).Select(row => new LibraryStockReconciliationRowDto(
			row.Id, row.AccessionNumber, finding, true, null, null, null, null, row.ObservedSectionId, row.ObservedShelfId, row.Sequence)), page, pageSize, maximumPageSize);
		var scanIds = result.Items.Select(row => row.Id).ToArray();
		var recentReceipts = source == LibraryStockReconciliationSource.OutsideBaseline && scanIds.Length > 0
			? await receiptCutoff.FindAsync(scope.TenantId, session.OpenedAt, scanned.Where(row => Enumerable.Contains(scanIds, row.Id)),
				(await SectionQueryAsync(scope)).Select(row => row.Id))
			: null;
		result.Items = result.Items.ConvertAll(row =>
		{
			var findingRow = row with { ReceivedAfterOpening = recentReceipts?.Contains(row.Id) == true };
			return findingRow with { EvidenceRevision = Fingerprint(new { session.Id, source, Row = findingRow }) };
		});
		return result;
	}

	private async Task<PaginatedResponse<LibraryStockReconciliationRowDto>> ReadExpectedReconciliationAsync(StockScope scope,
		LibraryStockVerificationSession session, IQueryable<LibraryStockVerificationBaseline> expected,
		IQueryable<LibraryStockVerificationObservation> scanned, int page, int pageSize, int maximumPageSize)
	{
		var expectedPage = await PageAsync(expected.OrderBy(row => row.AccessionNumber).ThenBy(row => row.CopyId), page, pageSize, maximumPageSize);
		var result = new PaginatedResponse<LibraryStockReconciliationRowDto>
		{
			CurrentPage = expectedPage.CurrentPage, PageSize = expectedPage.PageSize,
			TotalCount = expectedPage.TotalCount, TotalPages = expectedPage.TotalPages, Items = []
		};
		if (expectedPage.Items.Count == 0) return result;
		var ids = expectedPage.Items.Select(row => row.CopyId).ToArray();
		var matchedScans = (await AsyncExecuter.ToListAsync(LatestByCopy(scanned.Where(row => row.CopyId.HasValue &&
			Enumerable.Contains(ids, row.CopyId.Value))))).ToDictionary(row => row.CopyId!.Value);
		var current = (await copies.GetQueryableAsync()).Where(row => row.TenantId == scope.TenantId && Enumerable.Contains(ids, row.Id));
		if (!scope.TenantWide)
		{
			var locations = (await SectionQueryAsync(scope)).Select(row => row.Id);
			current = current.Where(row => locations.Contains(row.SectionId));
		}

		var register = (await AsyncExecuter.ToListAsync(current)).ToDictionary(row => row.Id);
		var visibleIds = register.Keys.ToArray();
		var accessions = register.Values.Select(row => row.NormalizedAccessionNumber).Distinct().ToArray();
		var loanRows = await AsyncExecuter.ToListAsync((await loans.GetQueryableAsync()).Where(row => row.TenantId == scope.TenantId &&
			row.Status == BookIssueStatus.Issued && (Enumerable.Contains(visibleIds, row.BookCopyId) ||
				Enumerable.Contains(accessions, row.AccessionNumber.Trim().ToUpper())))
			.Select(row => new { row.Id, row.BookCopyId, Accession = row.AccessionNumber.Trim().ToUpper() }));
		var loansByCopy = loanRows.ToLookup(row => row.BookCopyId);
		var loansByAccession = loanRows.ToLookup(row => row.Accession, StringComparer.Ordinal);
		var holdsByCopy = (await AsyncExecuter.ToListAsync((await holds.GetQueryableAsync()).Where(row => row.TenantId == scope.TenantId &&
			row.State == LibraryHoldState.Allocated && Enumerable.Contains(visibleIds, row.CopyId))
			.Select(row => new { row.Id, row.BookId, row.CopyId }))).ToLookup(row => row.CopyId);
		var custodyByCopy = (await AsyncExecuter.ToListAsync((await custody.GetQueryableAsync()).Where(row => row.TenantId == scope.TenantId &&
			!row.ReturnReceiptId.HasValue && Enumerable.Contains(visibleIds, row.CopyId))
			.Select(row => new { row.Id, row.BookId, row.CopyId, row.HomeSectionId, row.HomeShelfId }))).ToLookup(row => row.CopyId);
		var transferItemRows = await transferItems.GetQueryableAsync();
		var transferRows = await transfers.GetQueryableAsync();
		var transfersByCopy = (await AsyncExecuter.ToListAsync(from item in transferItemRows
			join transfer in transferRows on item.TransferId equals transfer.Id
			where item.TenantId == scope.TenantId && transfer.TenantId == scope.TenantId &&
				!item.ReceiptId.HasValue && Enumerable.Contains(visibleIds, item.CopyId)
			select new { item.Id, item.BookId, item.CopyId, item.SourceShelfId, transfer.SourceSectionId })).ToLookup(row => row.CopyId);

		foreach (var row in expectedPage.Items)
		{
			var copy = register.GetValueOrDefault(row.CopyId);
			var scan = matchedScans.GetValueOrDefault(row.CopyId);
			var loanCount = loansByCopy[row.CopyId].Count();
			var allocated = holdsByCopy[row.CopyId];
			var inCustody = custodyByCopy[row.CopyId];
			var inTransit = transfersByCopy[row.CopyId];
			var conflicting = loanCount > 1 || allocated.Count() > 1 || inCustody.Count() > 1 || inTransit.Count() > 1 || copy != null &&
				(loansByAccession[copy.NormalizedAccessionNumber].Any(loan => loan.BookCopyId != copy.Id) ||
				 allocated.Any(hold => hold.BookId != copy.BookId) ||
				 inCustody.Any(item => item.BookId != copy.BookId || item.HomeSectionId != copy.SectionId || item.HomeShelfId != copy.ShelfId) ||
				 inTransit.Any(item => item.BookId != copy.BookId || item.SourceSectionId != copy.SectionId || item.SourceShelfId != copy.ShelfId));
			var finding = LibraryStockReconciliation.Classify(session, row, copy, scan, loanCount == 1, allocated.Any(), conflicting, inCustody.Any(), inTransit.Any());
			var item = new LibraryStockReconciliationRowDto(row.CopyId, row.AccessionNumber, finding, LibraryStockReconciliation.RequiresReview(finding),
				row.CopyStatus, copy?.Status, row.SectionId, row.ShelfId, scan?.ObservedSectionId, scan?.ObservedShelfId, scan?.Sequence);
			var linkedLoans = loansByCopy[row.CopyId].Select(loan => loan.Id);
			if (copy != null)
				linkedLoans = linkedLoans.Union(loansByAccession[copy.NormalizedAccessionNumber].Select(loan => loan.Id));
			var revision = Fingerprint(new
			{
				session.Id, Row = item, row.BookId, ScanId = scan?.Id,
				Register = copy == null ? null : new { copy.BookId, copy.SectionId, copy.ShelfId, copy.NormalizedAccessionNumber },
				Loans = linkedLoans.Order().ToArray(), Holds = allocated.OrderBy(hold => hold.Id).ToArray()
			});
			if (inCustody.Any()) revision = Fingerprint(new { Revision = revision, Custody = inCustody.OrderBy(item => item.Id).ToArray() });
			if (inTransit.Any()) revision = Fingerprint(new { Revision = revision, Transfer = inTransit.OrderBy(item => item.Id).ToArray() });
			result.Items.Add(item with { EvidenceRevision = revision });
		}
		return result;
	}

	private static IQueryable<LibraryStockVerificationObservation> LatestByCopy(IQueryable<LibraryStockVerificationObservation> query)
	{
		var latest = query.GroupBy(row => row.CopyId).Select(group => group.Max(row => row.Sequence));
		return query.Where(row => latest.Contains(row.Sequence));
	}

	private static StockScope ReviewScope(StockScope scope, LibraryStockVerificationSession session) =>
		session.AcademicYearId is Guid year ? scope with { TenantWide = false, Years = [year] } : scope;
}

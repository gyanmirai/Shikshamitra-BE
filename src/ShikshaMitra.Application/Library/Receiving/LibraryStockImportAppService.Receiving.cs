using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;

namespace ShikshaMitra.Library.Receiving;

public partial class LibraryStockImportAppService
{
	private async Task<Dictionary<string, Guid>> CreateTitlesAsync(LibraryStockImportInput input, LibraryStockImportPlan plan)
	{
		var titleIds = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
		foreach (var group in plan.Rows.Where(row => row.NewTitle != null).GroupBy(row => row.TitleKey!, StringComparer.OrdinalIgnoreCase))
		{
			var request = group.First().NewTitle!.CreateRequest(input.CategoryId!.Value, plan.ConfigurationRevision);
			var title = await catalogue.CreateAsync(input.AcademicYearId, request);
			titleIds.Add(group.Key, Guid.Parse(title.Id));
		}
		return titleIds;
	}

	private async Task<List<LibraryStockImportRow>> ReceiveRowsAsync(Guid id, Guid tenant, LibraryStockImportInput input,
		LibraryStockImportPlan plan, Dictionary<string, Guid> titles)
	{
		var results = new List<LibraryStockImportRow>(plan.Rows.Count);
		var receiptNumber = 0;
		// Finish manual accessions first so the shared automatic allocator cannot take a value reserved in a later receipt.
		var receiptGroups = plan.Rows.GroupBy(row => (Automatic: row.Accession == null, row.ReceivedOn))
			.OrderBy(group => group.Key.Automatic).ThenBy(group => group.Key.ReceivedOn);
		foreach (var receiptGroup in receiptGroups)
		{
			var lineGroups = receiptGroup.GroupBy(row => (BookId: row.BookId ?? titles[row.TitleKey!], row.UnitCost, row.Currency));
			foreach (var part in lineGroups.Chunk(LibraryStockReceiptRules.MaximumLines))
			{
				var receipt = new LibraryReceivingInput
				{
					ReceivedOn = receiptGroup.Key.ReceivedOn, Source = input.Source,
					SourceName = LibraryStockImportPlanner.CleanSource(input.SourceName), Reference = $"Import {id:N}",
					Lines = part.Select(line => new LibraryReceivingLineInput
					{
						BookId = line.Key.BookId, SectionId = input.SectionId, ShelfId = input.ShelfId, Quantity = line.Count(),
						UnitCost = line.Key.UnitCost, Currency = line.Key.Currency,
						Accessions = line.Select(row => row.Accession ?? string.Empty).ToList()
					}).ToList()
				};
				var preview = await receiving.PreviewAsync(receipt);
				if (!preview.CanReceive) throw new BusinessException(LibraryErrorCodes.StockImportPlanChanged).WithData("Errors", preview.Errors);
				var committed = await receiving.ReceiveAsync(new()
				{
					Receipt = receipt, Revision = preview.Revision!, IdempotencyKey = $"import:{id:N}:{++receiptNumber}"
				});
				foreach (var (line, acceptedLine) in part.Zip(committed.Lines))
					foreach (var (row, copy) in line.Zip(acceptedLine.Copies))
						results.Add(new(GuidGenerator.Create(), tenant, id, row.Number, row.SourceId, acceptedLine.BookId,
							row.NewTitle?.Title ?? plan.ExistingTitles[acceptedLine.BookId], row.NewTitle != null,
							copy.CopyId, committed.Id, copy.AccessionNumber));
			}
		}
		if (results.Count != plan.Rows.Count) throw new BusinessException(LibraryErrorCodes.StockImportPlanChanged);
		return results;
	}
}

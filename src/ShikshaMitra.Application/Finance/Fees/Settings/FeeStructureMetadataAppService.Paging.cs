using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
using ShikshaMitra.Security.UserScope;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureMetadataAppService
{
	private sealed class MetadataClassReadRow : IUserScopedEntity
	{
		public Guid MetadataId { get; init; }
		public Guid? AcademicYearId { get; init; }
		public Guid? EducationBoardId { get; init; }
		public Guid? EducationMediumId { get; init; }
		public Guid? EducationMediumStageId { get; init; }
		public Guid? StandardId { get; init; }
		public Guid? DivisionId { get; init; }
		public string Name { get; init; } = "";
		public string StageName { get; init; } = "";
		public int StageSortOrder { get; init; }
		public int SortOrder { get; init; }
	}

	private async Task<FeeStructureMetadataListResponse> GetVisiblePageAsync(
		IQueryable<FeeStructureMetadata> query, int page, int pageSize, UserScopeContext scope, FinanceTableQuery filters)
	{
		var standards = await standardRepository.GetQueryableAsync();
		// Parent visibility uses the same accessible child-class facts as the previous
		// mapper; perform the scope predicate and parent existence test in SQL.
		var classes = (from metadata in query
			join standard in standards on metadata.AcademicMediumId equals (Guid?)standard.EducationMediumStage.EducationMediumId
			select new MetadataClassReadRow {
				MetadataId = metadata.Id, AcademicYearId = metadata.AcademicYearId,
				EducationBoardId = metadata.AcademicBoardId, EducationMediumId = metadata.AcademicMediumId,
				EducationMediumStageId = standard.EducationMediumStageId, StandardId = standard.Id, DivisionId = null,
				Name = standard.Name, SortOrder = standard.SortOrder,
				StageName = standard.EducationMediumStage.Name, StageSortOrder = standard.EducationMediumStage.SortOrder
			}).ApplyUserScope(scope);
		query = query.Where(metadata => classes.Any(c => c.MetadataId == metadata.Id));
		var structures = await feeStructureRepository.GetQueryableAsync();
		var derived = filters.Filters.Any(x => x.Column != "name");
		var totalCount = 0;
		IQueryable<FeeStructureMetadata> pageQuery;
		if (!derived)
		{
			totalCount = await AsyncExecuter.CountAsync(query);
			pageQuery = query.OrderByDescending(x => x.CreationTime).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize);
		}
		else
		{
			// Enum labels and the displayed configured/total ratio retain their exact
			// string semantics. Scan scalar batches, keeping only the requested page IDs.
			const int batchSize = 250;
			var selected = new List<Guid>();
			for (var offset = 0; ; offset += batchSize)
			{
				var batch = await AsyncExecuter.ToListAsync(query.OrderByDescending(x => x.CreationTime).ThenBy(x => x.Id)
					.Skip(offset).Take(batchSize).Select(x => new FeeStructureDetailsResponse {
						Id = x.Id, BoardName = x.AcademicBoard!.Type.ToString(), MediumName = x.AcademicMedium!.Name,
						TotalClasses = classes.Count(c => c.MetadataId == x.Id),
						ConfiguredClasses = classes.Count(c => c.MetadataId == x.Id && structures.Any(s =>
							s.FeeStructureMetadataId == x.Id && s.StandardId == c.StandardId && s.FeeStructureDetails.Any()))
					}));
				foreach (var row in filters.Apply(batch.AsQueryable(), MetadataDisplayFields, column => column != "name"))
				{
					if (totalCount >= (page - 1) * pageSize && selected.Count < pageSize) selected.Add(row.Id);
					totalCount++;
				}
				if (batch.Count < batchSize) break;
			}
			var ids = selected.ToArray();
			pageQuery = query.Where(x => Enumerable.Contains(ids, x.Id)).OrderByDescending(x => x.CreationTime).ThenBy(x => x.Id);
		}
		var items = await AsyncExecuter.ToListAsync(pageQuery.Select(x => new FeeStructureDetailsResponse {
			Id = x.Id, Name = x.Name, AcademicYearId = x.AcademicYearId!.Value, AcademicYearName = x.AcademicYear!.Name,
			BoardId = x.AcademicBoardId!.Value, BoardName = x.AcademicBoard!.Type.ToString(),
			MediumId = x.AcademicMediumId!.Value, MediumName = x.AcademicMedium!.Name,
			ReceiptBookId = x.ReceiptBookId, ReceiptBookName = x.ReceiptBook == null ? null : x.ReceiptBook.Name
		}));
		var pageIds = items.Select(x => x.Id).ToArray();
		var pageClasses = await AsyncExecuter.ToListAsync(classes.Where(c => Enumerable.Contains(pageIds, c.MetadataId)));
		var classesByMetadata = pageClasses.ToLookup(x => x.MetadataId);
		var configured = await AsyncExecuter.ToListAsync(structures
			.Where(x => Enumerable.Contains(pageIds, x.FeeStructureMetadataId) && x.FeeStructureDetails.Any())
			.Select(x => new { x.FeeStructureMetadataId, x.StandardId }).Distinct());
		var configuredByMetadata = configured.ToLookup(x => x.FeeStructureMetadataId, x => x.StandardId);
		foreach (var item in items)
		{
			item.Classes = classesByMetadata[item.Id].OrderBy(c => c.StageSortOrder).ThenBy(c => c.StageName).ThenBy(c => c.EducationMediumStageId)
				.ThenBy(c => c.SortOrder).ThenBy(c => c.Name).ThenBy(c => c.StandardId)
				.Select(c => new FeeStructureClassOptionDto { Id = c.StandardId!.Value, Name = c.Name }).ToList();
			var accessibleIds = item.Classes.Select(c => c.Id).ToHashSet();
			item.TotalClasses = accessibleIds.Count;
			item.ConfiguredClasses = configuredByMetadata[item.Id].Count(accessibleIds.Contains);
		}
		return new FeeStructureMetadataListResponse {
			TotalCount = totalCount, CurrentPage = page, PageSize = pageSize,
			TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize), Items = items
		};
	}
}

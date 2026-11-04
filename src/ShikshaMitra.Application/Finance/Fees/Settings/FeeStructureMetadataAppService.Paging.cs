using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureMetadataAppService
{
	private async Task<FeeStructureMetadataListResponse> GetVisiblePageAsync(
		IQueryable<FeeStructureMetadata> query, int page, int pageSize, Func<MetadataScope, bool> hasAccess)
	{
		// Visibility depends on child-class scope. Resolve only container identifiers
		// before paging; defer labels and structure configuration to the visible page.
		var identities = await AsyncExecuter.ToListAsync(query.Select(x => new {
			x.Id, YearId = x.AcademicYearId!.Value, BoardId = x.AcademicBoardId!.Value, MediumId = x.AcademicMediumId!.Value
		}));
		var mediumIds = identities.Select(x => x.MediumId).Distinct().ToArray();
		var classes = await AsyncExecuter.ToListAsync((await standardRepository.GetQueryableAsync())
			.Where(x => Enumerable.Contains(mediumIds, x.EducationMediumStage.EducationMediumId))
			.Select(x => new {
				MediumId = x.EducationMediumStage.EducationMediumId,
				StageId = x.EducationMediumStageId, StageName = x.EducationMediumStage.Name,
				StageSortOrder = x.EducationMediumStage.SortOrder, x.Id, x.Name, x.SortOrder
			}));
		var classesByMedium = classes.ToLookup(x => x.MediumId);
		var visibleIds = identities.Where(x => classesByMedium[x.MediumId].Any(c =>
			hasAccess(new MetadataScope(x.YearId, x.BoardId, x.MediumId, c.StageId, c.Id))))
			.Select(x => x.Id).ToArray();
		var items = await AsyncExecuter.ToListAsync(query.Where(x => Enumerable.Contains(visibleIds, x.Id))
			.OrderByDescending(x => x.CreationTime).ThenBy(x => x.Id)
			.Skip((page - 1) * pageSize).Take(pageSize)
			.Select(x => new FeeStructureDetailsResponse {
				Id = x.Id, Name = x.Name, AcademicYearId = x.AcademicYearId!.Value, AcademicYearName = x.AcademicYear!.Name,
				BoardId = x.AcademicBoardId!.Value, BoardName = x.AcademicBoard!.Type.ToString(),
				MediumId = x.AcademicMediumId!.Value, MediumName = x.AcademicMedium!.Name,
				ReceiptBookId = x.ReceiptBookId, ReceiptBookName = x.ReceiptBook == null ? null : x.ReceiptBook.Name
			}));
		var pageIds = items.Select(x => x.Id).ToArray();
		var configured = pageIds.Length == 0 ? [] : await AsyncExecuter.ToListAsync(
			(await feeStructureRepository.GetQueryableAsync())
				.Where(x => Enumerable.Contains(pageIds, x.FeeStructureMetadataId) && x.FeeStructureDetails.Any())
				.Select(x => new { x.FeeStructureMetadataId, x.StandardId }).Distinct());
		var configuredByMetadata = configured.ToLookup(x => x.FeeStructureMetadataId, x => x.StandardId);
		foreach (var item in items)
		{
			item.Classes = classesByMedium[item.MediumId]
				.Where(c => hasAccess(new MetadataScope(item.AcademicYearId, item.BoardId, item.MediumId, c.StageId, c.Id)))
				.OrderBy(c => c.StageSortOrder).ThenBy(c => c.StageName).ThenBy(c => c.StageId)
				.ThenBy(c => c.SortOrder).ThenBy(c => c.Name).ThenBy(c => c.Id)
				.Select(c => new FeeStructureClassOptionDto { Id = c.Id, Name = c.Name }).ToList();
			var accessibleIds = item.Classes.Select(c => c.Id).ToHashSet();
			item.TotalClasses = accessibleIds.Count;
			item.ConfiguredClasses = configuredByMetadata[item.Id].Count(accessibleIds.Contains);
		}
		return new FeeStructureMetadataListResponse {
			TotalCount = visibleIds.Length, CurrentPage = page, PageSize = pageSize,
			TotalPages = (int)Math.Ceiling(visibleIds.Length / (double)pageSize), Items = items
		};
	}
}

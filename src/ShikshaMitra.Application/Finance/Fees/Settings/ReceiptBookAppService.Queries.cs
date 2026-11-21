using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ShikshaMitra.Finance.Common;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.Dtos;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class ReceiptBookAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_ReceiptBook.View)]
	public async Task<ReceiptBookListResponse> GetListAsync(
		Guid academicYearId, int page = 1, int pageSize = 10, string? search = null, string? filters = null)
	{
		var tableQuery = ParseListFilters(filters);
		var query = await receiptBookRepository.GetQueryableAsync();

		query = query.Where(x => x.AcademicYearId == academicYearId);
		if (!string.IsNullOrWhiteSpace(search))
		{
			var normalizedSearch = search.Trim().ToLowerInvariant();
			query = query.Where(x => x.Name.ToLower().Contains(normalizedSearch));
		}

		query = await ApplyReceiptFiltersAsync(query, tableQuery);
		var totalCount = await AsyncExecuter.CountAsync(query);
		if (totalCount == 0)
			return new ReceiptBookListResponse
			{
				TotalCount = 0, CurrentPage = page, PageSize = pageSize,
				TotalPages = 0, Items = new List<ReceiptBookResponse>()
			};

		var dtos = await AsyncExecuter.ToListAsync(query.OrderByDescending(x => x.CreationTime).ThenBy(x => x.Id)
			.Skip((page - 1) * pageSize).Take(pageSize).Select(ResponseProjection));
		return new ReceiptBookListResponse
		{
			TotalCount  = totalCount,
			CurrentPage = page,
			PageSize    = pageSize,
			TotalPages  = (int)Math.Ceiling(totalCount / (double)pageSize),
			Items       = dtos,
		};
	}

	private static readonly Expression<Func<ReceiptBook, ReceiptBookResponse>> ResponseProjection = receiptBook => new()
	{
		Id             = receiptBook.Id,
		Name           = receiptBook.Name,
		Prefix         = receiptBook.Prefix,
		Suffix         = receiptBook.Suffix,
		BoardId        = receiptBook.BoardId,
		MediumId       = receiptBook.MediumId,
		MediumStageId  = receiptBook.MediumStageId,
		StartingNumber = receiptBook.StartingNumber,
		EndingNumber   = receiptBook.EndingNumber,
		Description    = receiptBook.Description,
		IsActive       = receiptBook.IsActive,
		CreatedAt      = receiptBook.CreationTime,
		BoardName      = receiptBook.EducationBoard == null ? string.Empty : receiptBook.EducationBoard.Type.ToString(),
		MediumName     = receiptBook.EducationMedium == null ? string.Empty : receiptBook.EducationMedium.Name,
		MediumStageName = receiptBook.EducationMediumStage == null ? string.Empty : receiptBook.EducationMediumStage.Name,
	};
	private static readonly Func<ReceiptBook, ReceiptBookResponse> ResponseMapper = ResponseProjection.Compile();
	private static ReceiptBookResponse MapToResponse(ReceiptBook receiptBook) => ResponseMapper(receiptBook);



	private static readonly IReadOnlyDictionary<string, LambdaExpression> ReceiptSqlFields = new Dictionary<string, LambdaExpression>
	{
		["name"] = (Expression<Func<ReceiptBook, string>>)(x => x.Name),
		["prefix"] = (Expression<Func<ReceiptBook, string>>)(x => x.Prefix),
		["suffix"] = (Expression<Func<ReceiptBook, string>>)(x => x.Suffix),
		["mediumStageName"] = (Expression<Func<ReceiptBook, string>>)(x => x.EducationMediumStage == null ? "" : x.EducationMediumStage.Name),
		["description"] = (Expression<Func<ReceiptBook, string>>)(x => x.Description),
		["startingNumber"] = (Expression<Func<ReceiptBook, long>>)(x => x.StartingNumber),
		["endingNumber"] = (Expression<Func<ReceiptBook, long>>)(x => x.EndingNumber),
		["isActive"] = (Expression<Func<ReceiptBook, int>>)(x => x.IsActive ? 1 : 0)
	};
	private async Task<IQueryable<ReceiptBook>> ApplyReceiptFiltersAsync(IQueryable<ReceiptBook> query, FinanceTableQuery filters)
	{
		query = filters.Apply(query, ReceiptSqlFields, column => column != "board");
		if (!filters.Filters.Any(x => x.Column == "board")) return query;
		var ids = new List<Guid>();
		for (var skip = 0; ; skip += 256)
		{
			var batch = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.Id).Skip(skip).Take(256)
				.Select(x => new { x.Id, Board = x.EducationBoard.Type, Medium = x.EducationMedium.Name }));
			var displayed = batch.Select(x => new ReceiptBookResponse { Id = x.Id, BoardName = x.Board.ToString(), MediumName = x.Medium });
			ids.AddRange(filters.Apply(displayed.AsQueryable(), ListFilterFields, column => column == "board").Select(x => x.Id));
			if (batch.Count < 256) break;
		}
		var selectedIds = ids.ToArray();
		return query.Where(x => Enumerable.Contains(selectedIds, x.Id));
	}
}

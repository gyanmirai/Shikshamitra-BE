using System;
using System.Collections.Generic;
using System.Linq;
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
		var hasHeaderFilters = tableQuery.Filters.Count > 0;
		var query = await receiptBookRepository.WithDetailsAsync(
			x => x.EducationBoard, x => x.EducationMedium, x => x.EducationMediumStage);

		query = query.Where(x => x.AcademicYearId == academicYearId);
		if (!string.IsNullOrWhiteSpace(search))
			query = query.Where(x => x.Name.Contains(search));

		var totalCount = await AsyncExecuter.CountAsync(query);
		if (totalCount == 0)
			return new ReceiptBookListResponse
			{
				TotalCount = 0, CurrentPage = page, PageSize = pageSize,
				TotalPages = 0, Items = new List<ReceiptBookResponse>()
			};

		query = query.OrderByDescending(x => x.CreationTime);
		if (!hasHeaderFilters)
			query = query.Skip((page - 1) * pageSize).Take(pageSize);

		var items = await AsyncExecuter.ToListAsync(query);
		var dtos = items.Select(MapToResponse).ToList();
		if (hasHeaderFilters)
		{
			dtos = tableQuery.Apply(dtos.AsQueryable(), ListFilterFields).ToList();
			totalCount = dtos.Count;
			dtos = dtos.Skip((page - 1) * pageSize).Take(pageSize).ToList();
		}
		return new ReceiptBookListResponse
		{
			TotalCount  = totalCount,
			CurrentPage = page,
			PageSize    = pageSize,
			TotalPages  = (int)Math.Ceiling(totalCount / (double)pageSize),
			Items       = dtos,
		};
	}

	private static ReceiptBookResponse MapToResponse(ReceiptBook receiptBook) => new()
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
		BoardName      = receiptBook.EducationBoard?.Type.ToString() ?? string.Empty,
		MediumName     = receiptBook.EducationMedium?.Name ?? string.Empty,
		MediumStageName = receiptBook.EducationMediumStage?.Name ?? string.Empty,
	};
}

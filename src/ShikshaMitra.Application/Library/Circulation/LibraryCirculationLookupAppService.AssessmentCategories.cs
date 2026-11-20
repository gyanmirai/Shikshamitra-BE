using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Shared;

namespace ShikshaMitra.Library.Circulation;

public partial class LibraryCirculationLookupAppService
{
	public async Task<PaginatedResponse<LibraryReturnAssessmentCategoryDto>> GetAssessmentCategoriesAsync(Guid academicYearId, int page = 1, int pageSize = 20, string? search = null)
	{
		await EnsurePermissionAsync(ShikshaMitraSubmodulePermissions.Library.IssueReturn.Return);
		await access.GetContextAsync(academicYearId);
		var query = (await assessmentCategories.GetQueryableAsync())
			.Where(row => row.TenantId == CurrentTenant.Id && row.AcademicYearId == academicYearId);
		var term = (search ?? string.Empty).Trim().ToLowerInvariant();
		if (term.Length > 0) query = query.Where(row => row.ChargeType.ToLower().Contains(term));
		page = Math.Max(1, page);
		pageSize = Math.Clamp(pageSize, 1, 100);
		var count = await AsyncExecuter.LongCountAsync(query);
		var offset = ((long)page - 1) * pageSize;
		var items = offset >= count ? new List<LibraryReturnAssessmentCategoryDto>() :
			await AsyncExecuter.ToListAsync(query.OrderBy(row => row.ChargeType).ThenBy(row => row.Id)
				.Skip((int)offset).Take(pageSize).Select(row => new LibraryReturnAssessmentCategoryDto
				{
					Id = row.Id, AcademicYearId = row.AcademicYearId, Name = row.ChargeType,
					Amount = row.Amount, IsFixed = row.IsFixed
				}));
		return new()
		{
			TotalCount = count, CurrentPage = page, PageSize = pageSize,
			TotalPages = (int)Math.Ceiling(count / (double)pageSize), Items = items
		};
	}
}

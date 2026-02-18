using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Setting.BorrowingRules.Dtos;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library.Setting.BorrowingRules;

[RemoteService(false)]
public class BorrowingRuleAppService(IRepository<BorrowingRule, Guid> _repository)
	: ApplicationService, IBorrowingRuleAppService
{
	private static readonly string[] RoleNames = { "Student", "Teacher" };

	public async Task<PaginatedResponse<BorrowingRuleDto>> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10)
	{
		var query = await _repository.GetQueryableAsync();
		query = query.Where(x => x.AcademicYearId == academicYearId);

		var totalCount = await AsyncExecuter.CountAsync(query);
		var totalPages = (int) Math.Ceiling(totalCount / (double) pageSize);

		var items = await AsyncExecuter.ToListAsync(
			query
				.OrderBy(x => x.Role)
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
		);

		var dtos = items.Select(x => new BorrowingRuleDto
		{
			Id = x.Id,
			Role = x.Role < RoleNames.Length ? RoleNames[x.Role] : x.Role.ToString(),
			MaxBooksAtTime = x.MaxBooksAtTime,
			ReservationLimit = x.ReservationLimit
		}).ToList();

		return new PaginatedResponse<BorrowingRuleDto>
		{
			TotalCount = totalCount,
			CurrentPage = page,
			PageSize = pageSize,
			TotalPages = totalPages,
			Items = dtos
		};
	}

	public async Task<GenericUpdatedResponse> UpdateRulesAsync(Guid academicYearId, BorrowingRulesUpdateRequest input)
	{
		var query = await _repository.GetQueryableAsync();
		var existing = await AsyncExecuter.ToListAsync(
			query.Where(x => x.AcademicYearId == academicYearId)
		);

		foreach (var ruleItem in input.Rules)
		{
			var record = existing.FirstOrDefault(x => x.Role == ruleItem.Role);
			if (record != null)
			{
				record.MaxBooksAtTime = ruleItem.MaxBooksAtTime;
				record.ReservationLimit = ruleItem.ReservationLimit;
				await _repository.UpdateAsync(record);
			}
			else
			{
				var newRecord = new BorrowingRule(
					GuidGenerator.Create(),
					CurrentTenant.Id,
					academicYearId,
					ruleItem.Role,
					ruleItem.MaxBooksAtTime,
					ruleItem.ReservationLimit
				);
				await _repository.InsertAsync(newRecord);
			}
		}

		return new GenericUpdatedResponse
		{
			Id = academicYearId.ToString(),
			Message = "Borrowing rules updated successfully."
		};
	}
}

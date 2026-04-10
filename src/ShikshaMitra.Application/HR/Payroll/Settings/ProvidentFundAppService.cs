using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.Settings;

[RemoteService(false)]
public class ProvidentFundAppService(IProvidentFundRepository providentFundRepository)
	: CrudAppService<HR.Payroll.Settings.ProvidentFund, ProvidentFundDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateProvidentFundDto>(providentFundRepository),
		IProvidentFundAppService
{
	private readonly IProvidentFundRepository _providentFundRepository = providentFundRepository;

	public override async Task<ProvidentFundDto> CreateAsync(CreateUpdateProvidentFundDto input)
	{
		ArgumentNullException.ThrowIfNull(input);

		await EnsureCodeAvailableAsync(input.Code);

		try
		{
			return await base.CreateAsync(input);
		}
		catch (Exception ex) when (IsProvidentFundCodeUniqueViolation(ex))
		{
			throw new BusinessException("ShikshaMitra:ProvidentFundCodeAlreadyExists")
				.WithData("code", input.Code);
		}
	}

	public override async Task<ProvidentFundDto> UpdateAsync(Guid id, CreateUpdateProvidentFundDto input)
	{
		ArgumentNullException.ThrowIfNull(input);

		var existing = await Repository.GetAsync(id);
		if (!existing.Code.Equals(input.Code, StringComparison.OrdinalIgnoreCase))
		{
			await EnsureCodeAvailableAsync(input.Code, id);
		}

		try
		{
			return await base.UpdateAsync(id, input);
		}
		catch (Exception ex) when (IsProvidentFundCodeUniqueViolation(ex))
		{
			throw new BusinessException("ShikshaMitra:ProvidentFundCodeAlreadyExists")
				.WithData("code", input.Code);
		}
	}

	public async Task<PagedResultDto<ProvidentFundDto>> GetFilteredListAsync(int skipCount, int maxResultCount, string? search)
	{
		var queryable = await Repository.GetQueryableAsync();

		if (!string.IsNullOrWhiteSpace(search))
		{
			var term = search.Trim().ToLower();
			queryable = queryable.Where(x =>
				x.Name.ToLower().Contains(term) ||
				x.Code.ToLower().Contains(term));
		}

		queryable = queryable.OrderBy(x => x.Name);

		var totalCount = await AsyncExecuter.CountAsync(queryable);
		var items = await AsyncExecuter.ToListAsync(
			queryable
				.Skip(skipCount)
				.Take(maxResultCount)
		);

		return new PagedResultDto<ProvidentFundDto>(
			totalCount,
			ObjectMapper.Map<List<ProvidentFund>, List<ProvidentFundDto>>(items)
		);
	}

	private async Task EnsureCodeAvailableAsync(string code, Guid? excludeId = null)
	{
		if (await _providentFundRepository.IsCodeExistsAsync(code, excludeId))
		{
			throw new BusinessException("ShikshaMitra:ProvidentFundCodeAlreadyExists")
				.WithData("code", code);
		}
	}

	private static bool IsProvidentFundCodeUniqueViolation(Exception exception)
	{
		const string indexName = "IX_ProvidentFunds_TenantId_Code";
		const string tableHint = "AppProvidentFunds";

		for (var ex = exception; ex != null; ex = ex.InnerException!)
		{
			var message = ex.Message;
			if (string.IsNullOrEmpty(message))
			{
				continue;
			}

			if (message.Contains(indexName, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			if (message.Contains(tableHint, StringComparison.OrdinalIgnoreCase)
			    && message.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			if (message.Contains("23505", StringComparison.Ordinal)
			    && (message.Contains(tableHint, StringComparison.OrdinalIgnoreCase)
			        || message.Contains(indexName, StringComparison.OrdinalIgnoreCase)))
			{
				return true;
			}
		}

		return false;
	}
}

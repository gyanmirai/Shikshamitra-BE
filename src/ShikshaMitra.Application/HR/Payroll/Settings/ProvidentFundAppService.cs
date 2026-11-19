using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Permissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.HR.PayrollManagement_SettingsPfRule;

namespace ShikshaMitra.HR.Payroll.Settings;

[RemoteService(false)]
public class ProvidentFundAppService(
	IProvidentFundRepository providentFundRepository,
	PayrollStatutoryRuleInvalidator statutoryRuleInvalidator)
	: CrudAppService<HR.Payroll.Settings.ProvidentFund, ProvidentFundDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateProvidentFundDto>(providentFundRepository),
		IProvidentFundAppService
{
	private readonly IProvidentFundRepository _providentFundRepository = providentFundRepository;
	protected override Task CheckGetPolicyAsync() => AuthorizationService.CheckAsync(Permissions.View);
	protected override Task CheckGetListPolicyAsync() => AuthorizationService.CheckAsync(Permissions.View);
	protected override Task CheckCreatePolicyAsync() => AuthorizationService.CheckAsync(Permissions.Add);
	protected override Task CheckUpdatePolicyAsync() => AuthorizationService.CheckAsync(Permissions.Add);
	protected override Task CheckDeletePolicyAsync() => AuthorizationService.CheckAsync(Permissions.Delete);

	public override async Task<ProvidentFundDto> CreateAsync(CreateUpdateProvidentFundDto input)
	{
		await CheckCreatePolicyAsync();
		ArgumentNullException.ThrowIfNull(input);

		await EnsureCodeAvailableAsync(input.Code);
		await EnsureNameAvailableAsync(input.Name);

		try
		{
			var entity = new ProvidentFund(
				GuidGenerator.Create(), CurrentTenant.Id, input.Code.Trim(), input.Name.Trim(),
				input.EmployeePercentage, input.EmployerPercentage, input.ContributionBasis,
				input.WageCeiling, input.EffectiveFrom, input.EffectiveTo, input.IsActive);
			await Repository.InsertAsync(entity, autoSave: true);
			return ObjectMapper.Map<ProvidentFund, ProvidentFundDto>(entity);
		}
		catch (Exception ex) when (IsProvidentFundCodeUniqueViolation(ex))
		{
			throw new BusinessException(HRErrorCodes.ProvidentFundCodeAlreadyExists)
				.WithData("code", input.Code);
		}
		catch (Exception ex) when (IsProvidentFundNameUniqueViolation(ex))
		{
			throw new BusinessException(HRErrorCodes.ProvidentFundNameAlreadyExists)
				.WithData("name", input.Name);
		}
	}

	public override async Task<ProvidentFundDto> UpdateAsync(Guid id, CreateUpdateProvidentFundDto input)
	{
		await CheckUpdatePolicyAsync();
		ArgumentNullException.ThrowIfNull(input);

		var existing = await Repository.GetAsync(id);
		if (!existing.Code.Equals(input.Code, StringComparison.OrdinalIgnoreCase))
		{
			await EnsureCodeAvailableAsync(input.Code, id);
		}

		if (!existing.Name.Equals(input.Name, StringComparison.OrdinalIgnoreCase))
		{
			await EnsureNameAvailableAsync(input.Name, id);
		}

		try
		{
			existing.Update(
				input.Code, input.Name, input.EmployeePercentage, input.EmployerPercentage,
				input.ContributionBasis, input.WageCeiling, input.EffectiveFrom,
				input.EffectiveTo, input.IsActive);
			await Repository.UpdateAsync(existing, autoSave: true);
			await statutoryRuleInvalidator.InvalidateProvidentFundAsync(id);
			return ObjectMapper.Map<ProvidentFund, ProvidentFundDto>(existing);
		}
		catch (Exception ex) when (IsProvidentFundCodeUniqueViolation(ex))
		{
			throw new BusinessException(HRErrorCodes.ProvidentFundCodeAlreadyExists)
				.WithData("code", input.Code);
		}
		catch (Exception ex) when (IsProvidentFundNameUniqueViolation(ex))
		{
			throw new BusinessException(HRErrorCodes.ProvidentFundNameAlreadyExists)
				.WithData("name", input.Name);
		}
	}

	public override async Task DeleteAsync(Guid id)
	{
		await base.DeleteAsync(id);
		await statutoryRuleInvalidator.InvalidateProvidentFundAsync(id);
	}

	public async Task<PagedResultDto<ProvidentFundDto>> GetFilteredListAsync(int skipCount, int maxResultCount, string? search)
	{
		await CheckGetListPolicyAsync();
		var queryable = await Repository.GetQueryableAsync();

		if (!string.IsNullOrWhiteSpace(search))
		{
			var term = search.Trim().ToLower();
			queryable = queryable.Where(x =>
				x.Name.ToLower().Contains(term) ||
				x.Code.ToLower().Contains(term));
		}

		queryable = queryable.OrderBy(x => x.Name).ThenBy(x => x.Id);

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
			throw new BusinessException(HRErrorCodes.ProvidentFundCodeAlreadyExists)
				.WithData("code", code);
		}
	}

	private async Task EnsureNameAvailableAsync(string name, Guid? excludeId = null)
	{
		if (await _providentFundRepository.IsNameExistsAsync(name, excludeId))
		{
			throw new BusinessException(HRErrorCodes.ProvidentFundNameAlreadyExists)
				.WithData("name", name);
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

	private static bool IsProvidentFundNameUniqueViolation(Exception exception)
	{
		const string indexName = "IX_ProvidentFunds_TenantId_Name";
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

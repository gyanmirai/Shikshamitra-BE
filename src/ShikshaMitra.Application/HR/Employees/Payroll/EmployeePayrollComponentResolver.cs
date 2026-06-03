using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.Settings;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Employees.Payroll;

public class EmployeePayrollComponentResolver(
	IEarningComponentRepository earningComponentRepository,
	IDeductionComponentRepository deductionComponentRepository,
	IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
	public async Task<List<EmployeePayrollResolvedLine<EarningComponent>>> ResolveEarningsAsync(
		IEnumerable<SalaryComponentContract>? lines,
		IEnumerable<EmployeePayrollComponent> existingEarnings)
	{
		var resolvedLines = new List<EmployeePayrollResolvedLine<EarningComponent>>();
		foreach (var line in lines ?? [])
		{
			resolvedLines.Add(await ResolveEarningLineAsync(line, existingEarnings));
		}

		return resolvedLines;
	}

	public async Task<List<EmployeePayrollResolvedLine<DeductionComponent>>> ResolveDeductionsAsync(
		IEnumerable<SalaryComponentContract>? lines,
		IEnumerable<EmployeePayrollComponent> existingDeductions)
	{
		var resolvedLines = new List<EmployeePayrollResolvedLine<DeductionComponent>>();
		foreach (var line in lines ?? [])
		{
			resolvedLines.Add(await ResolveDeductionLineAsync(line, existingDeductions));
		}

		return resolvedLines;
	}

	private async Task<EmployeePayrollResolvedLine<EarningComponent>> ResolveEarningLineAsync(
		SalaryComponentContract line,
		IEnumerable<EmployeePayrollComponent> existingEarnings)
	{
		var existingPayrollRow = FindExistingPayrollRow(line, existingEarnings);
		if (line.ComponentId is { } componentId && componentId != Guid.Empty)
		{
			try
			{
				var master = await earningComponentRepository.GetAsync(componentId);
				EmployeePayrollRules.EnsureActiveMaster(master, HRErrorCodes.EmployeePayrollInvalidEarningComponent, componentId);
				return new EmployeePayrollResolvedLine<EarningComponent>(line, master, existingPayrollRow);
			}
			catch (EntityNotFoundException)
			{
				if (existingPayrollRow?.ComponentId is { } masterId && masterId != Guid.Empty)
				{
					try
					{
						var master = await earningComponentRepository.GetAsync(masterId);
						EmployeePayrollRules.EnsureActiveMaster(master, HRErrorCodes.EmployeePayrollInvalidEarningComponent, masterId);
						return new EmployeePayrollResolvedLine<EarningComponent>(line, master, existingPayrollRow);
					}
					catch (EntityNotFoundException)
					{
						// Preserve the current forged-id error path for the submitted component id.
					}
				}

				if (existingPayrollRow is not null &&
				    EmployeePayrollRules.IsCustomPayrollRow(existingPayrollRow) &&
				    componentId == existingPayrollRow.Id)
				{
					return new EmployeePayrollResolvedLine<EarningComponent>(line, null, existingPayrollRow);
				}

				throw new BusinessException(HRErrorCodes.EmployeePayrollInvalidEarningComponent)
					.WithData("componentId", componentId);
			}
		}

		if (line.IsTemp == true || (existingPayrollRow is not null && EmployeePayrollRules.IsCustomPayrollRow(existingPayrollRow)))
		{
			return new EmployeePayrollResolvedLine<EarningComponent>(line, null, existingPayrollRow);
		}

		var normalizedName = (line.Name ?? string.Empty).Trim().ToUpperInvariant();
		var matches = await asyncExecuter.ToListAsync(
			(await earningComponentRepository.GetQueryableAsync())
				.Where(x => x.IsActive && x.Name.ToUpper() == normalizedName));
		if (matches.Count != 1)
		{
			return new EmployeePayrollResolvedLine<EarningComponent>(line, null, existingPayrollRow);
		}

		return new EmployeePayrollResolvedLine<EarningComponent>(line, matches[0], existingPayrollRow);
	}

	private async Task<EmployeePayrollResolvedLine<DeductionComponent>> ResolveDeductionLineAsync(
		SalaryComponentContract line,
		IEnumerable<EmployeePayrollComponent> existingDeductions)
	{
		var existingPayrollRow = FindExistingPayrollRow(line, existingDeductions);
		if (line.ComponentId is { } componentId && componentId != Guid.Empty)
		{
			try
			{
				var master = await deductionComponentRepository.GetAsync(componentId);
				EmployeePayrollRules.EnsureActiveMaster(master, HRErrorCodes.EmployeePayrollInvalidDeductionComponent, componentId);
				return new EmployeePayrollResolvedLine<DeductionComponent>(line, master, existingPayrollRow);
			}
			catch (EntityNotFoundException)
			{
				if (existingPayrollRow?.ComponentId is { } masterId && masterId != Guid.Empty)
				{
					try
					{
						var master = await deductionComponentRepository.GetAsync(masterId);
						EmployeePayrollRules.EnsureActiveMaster(master, HRErrorCodes.EmployeePayrollInvalidDeductionComponent, masterId);
						return new EmployeePayrollResolvedLine<DeductionComponent>(line, master, existingPayrollRow);
					}
					catch (EntityNotFoundException)
					{
						// Preserve the current forged-id error path for the submitted component id.
					}
				}

				if (existingPayrollRow is not null &&
				    EmployeePayrollRules.IsCustomPayrollRow(existingPayrollRow) &&
				    componentId == existingPayrollRow.Id)
				{
					return new EmployeePayrollResolvedLine<DeductionComponent>(line, null, existingPayrollRow);
				}

				throw new BusinessException(HRErrorCodes.EmployeePayrollInvalidDeductionComponent)
					.WithData("componentId", componentId);
			}
		}

		if (line.IsTemp == true || (existingPayrollRow is not null && EmployeePayrollRules.IsCustomPayrollRow(existingPayrollRow)))
		{
			return new EmployeePayrollResolvedLine<DeductionComponent>(line, null, existingPayrollRow);
		}

		var normalizedName = (line.Name ?? string.Empty).Trim().ToUpperInvariant();
		var matches = await asyncExecuter.ToListAsync(
			(await deductionComponentRepository.GetQueryableAsync())
				.Where(x => x.IsActive && x.Name.ToUpper() == normalizedName));
		if (matches.Count != 1)
		{
			return new EmployeePayrollResolvedLine<DeductionComponent>(line, null, existingPayrollRow);
		}

		return new EmployeePayrollResolvedLine<DeductionComponent>(line, matches[0], existingPayrollRow);
	}

	private static EmployeePayrollComponent? FindExistingPayrollRow(
		SalaryComponentContract line,
		IEnumerable<EmployeePayrollComponent> existingComponents)
	{
		if (line.Id is { } id && id != Guid.Empty)
		{
			var byId = existingComponents.FirstOrDefault(x => x.Id == id);
			if (byId is not null)
			{
				return byId;
			}
		}

		if (line.ComponentId is { } componentId && componentId != Guid.Empty)
		{
			return existingComponents.FirstOrDefault(x => x.Id == componentId);
		}

		return null;
	}
}

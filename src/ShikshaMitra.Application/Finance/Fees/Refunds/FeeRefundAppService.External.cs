using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Library.Books;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

public partial class FeeRefundAppService
{
	private FinanceExternalRefundRequestAccess ExternalRefundAccess =>
		LazyServiceProvider.LazyGetRequiredService<FinanceExternalRefundRequestAccess>();

	private FinanceExternalRefundWorkflow ExternalRefundWorkflow =>
		LazyServiceProvider.LazyGetRequiredService<FinanceExternalRefundWorkflow>();

	private async Task EnsureExternalRefundActionAsync(FeeRefundRequest request, string action)
	{
		await ExternalRefundAccess.RequireAsync(request, action);
		await LazyServiceProvider.LazyGetRequiredService<ILibraryInventoryLock>().AcquireAsync(CurrentTenant.Id);
	}

	private async Task PopulateEmployeeRefundNamesAsync(IEnumerable<FeeRefundRequestDto> items)
	{
		var dtos = items.ToArray();
		var ids = dtos.Where(item => item.EmployeeId.HasValue).Select(item => item.EmployeeId!.Value).Distinct().ToArray();
		if (ids.Length == 0) return;
		var employees = await AsyncExecuter.ToListAsync((await LazyServiceProvider
			.LazyGetRequiredService<IRepository<Employee, Guid>>().GetQueryableAsync()).Where(employee =>
			employee.TenantId == CurrentTenant.Id && Enumerable.Contains(ids, employee.Id))
			.Select(employee => new { employee.Id, employee.FirstName, employee.MiddleName, employee.LastName }));
		var names = employees.ToDictionary(employee => employee.Id, employee => string.Join(" ",
			new[] { employee.FirstName, employee.MiddleName, employee.LastName }.Where(part => !string.IsNullOrWhiteSpace(part))));
		foreach (var dto in dtos.Where(item => item.EmployeeId.HasValue))
			dto.PayerName = names.GetValueOrDefault(dto.EmployeeId!.Value);
	}

	private async Task<List<PaidChargeRefundSource>> ReadExternalRefundEvidenceAsync(FeeRefundRequest request)
	{
		try { return await ExternalRefundWorkflow.ReadSourcesAsync(request); }
		catch (BusinessException error) when (error.Code == FeeRefundErrorCodes.SourceReceiptInvalid ||
			error.Code == FeeRefundErrorCodes.PolicyVersionInvalid)
		{
			// Authorized history remains readable; a missing source cannot be reported as reconciled.
			return [];
		}
	}
}

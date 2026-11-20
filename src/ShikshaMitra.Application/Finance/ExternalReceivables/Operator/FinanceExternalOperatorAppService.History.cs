using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Receipts;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Finance.ExternalReceivables.Operator;

public partial class FinanceExternalOperatorAppService
{
	private async Task<PagedResultDto<FinanceExternalOperatorHistoryDto>> HistoryAsync(FinanceExternalSourceReference source,
		int page, int pageSize)
	{
		var context = await authority.RequireAsync(source, ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.View);
		var offset = Offset(page, pageSize);
		var query = (await LazyServiceProvider.LazyGetRequiredService<IRepository<FinanceExternalReceivableCommand, Guid>>().GetQueryableAsync())
			.Where(row => row.TenantId == CurrentTenant.Id && row.SourceLinkId == context.Link.Id);
		var count = await AsyncExecuter.CountAsync(query);
		if (offset >= count) return new(count, []);
		var commands = await AsyncExecuter.ToListAsync(query.OrderByDescending(row => row.CreationTime).ThenBy(row => row.Id)
			.Skip((int)offset).Take(pageSize));
		var actorIds = commands.Select(row => row.ActorId).Distinct().ToArray();
		var names = await AsyncExecuter.ToListAsync((await LazyServiceProvider.LazyGetRequiredService<IRepository<IdentityUser, Guid>>()
			.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id && Enumerable.Contains(actorIds, row.Id))
			.Select(row => new { row.Id, row.UserName }));
		var canReadReceipt = await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_InvoiceReceipt.View);
		var receipts = LazyServiceProvider.LazyGetRequiredService<IRepository<FeePaymentReceipt, Guid>>();
		var items = new List<FinanceExternalOperatorHistoryDto>();
		foreach (var command in commands)
		{
			var row = new FinanceExternalOperatorHistoryDto
			{
				Id = command.Id, Operation = command.Operation, RecordedAt = new DateTimeOffset(DateTime.SpecifyKind(command.CreationTime, DateTimeKind.Utc)),
				ActorName = names.FirstOrDefault(actor => actor.Id == command.ActorId)?.UserName,
				ResultKind = command.ResultEntityType, ResultId = command.ResultEntityId
			};
			if (canReadReceipt && command.Operation == "Collect" && command.ResultEntityType == "Receipt" && command.ResultEntityId.HasValue)
			{
				var receipt = await receipts.FindAsync(command.ResultEntityId.Value, includeDetails: false);
				if (receipt != null && receipt.TenantId == CurrentTenant.Id && receipt.AcademicYearId == context.Link.AcademicYearId &&
					receipt.StudentId == context.Source.Payer.StudentId && receipt.EmployeeId == context.Source.Payer.EmployeeId &&
					!receipt.FinanceFeeAccountId.HasValue && receipt.Currency == context.Link.Currency)
				{
					try { await scope.EnsureReceiptAccessibleAsync(receipt); row.CanOpenResult = true; }
					catch (AbpAuthorizationException) { }
					catch (BusinessException) { }
				}
			}
			items.Add(row);
		}
		return new(count, items);
	}
}

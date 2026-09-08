using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Audit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;

namespace ShikshaMitra.Domain.Finance.Policies;

[RemoteService(false)]
[Authorize]
public class CollectionPolicyExceptionWindowAppService(
	IRepository<CollectionPolicyExceptionWindow, Guid> repository,
	FinanceAuditWriter financeAuditWriter,
	IClock clock
) : ApplicationService, ICollectionPolicyExceptionWindowAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.ManageExceptionWindow)]
	public async Task<CollectionPolicyExceptionWindowDto> CreateAsync(CreateCollectionPolicyExceptionWindowDto input)
	{
		var scope = ParseScope(input.Scope);
		var effect = ParseEffect(input.Effect);
		var existingQuery = await repository.GetQueryableAsync();
		if (existingQuery.Any(x => x.Scope == scope && x.ScopeId == (scope == ExceptionWindowScope.SchoolWide ? null : input.ScopeId) &&
			x.StartDate <= input.EndDate.Date && x.EndDate >= input.StartDate.Date))
		{
			throw new BusinessException(CollectionPolicyErrorCodes.ExceptionWindowAlreadyActive)
				.WithData("reason", "overlapping_window");
		}
		var entity = new CollectionPolicyExceptionWindow(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			scope,
			input.ScopeId,
			input.StartDate,
			input.EndDate,
			effect,
			input.ExtensionDays,
			input.ReasonCode,
			input.Reason,
			CurrentUser.Id ?? Guid.Empty);

		await repository.InsertAsync(entity, autoSave: false);

		// The window's own creation is audit-logged; individual due lines inside it are not,
		// since nothing was charged to reverse (implementation plan §4j data-model bullet).
		await financeAuditWriter.WriteAsync(
			entity.Scope == ExceptionWindowScope.AcademicYear ? entity.ScopeId : null,
			FinanceAuditEntityTypes.CollectionPolicyExceptionWindow,
			entity.Id,
			$"{entity.Scope} exception window ({entity.StartDate:yyyy-MM-dd} to {entity.EndDate:yyyy-MM-dd})",
			FinanceAuditActions.Create,
			$"{entity.Effect} exception window created for {entity.Scope}.",
			null,
			entity.Reason,
			null,
			new { entity.Scope, entity.ScopeId, entity.StartDate, entity.EndDate, entity.Effect, entity.ExtensionDays },
			$"cpew-{entity.Id:N}");

		await CurrentUnitOfWork!.SaveChangesAsync();

		return ToDto(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.View)]
	public async Task<List<CollectionPolicyExceptionWindowDto>> GetListAsync(CollectionPolicyExceptionWindowListRequestDto input)
	{
		var query = await repository.GetQueryableAsync();

		if (!string.IsNullOrWhiteSpace(input.Scope))
		{
			var scope = ParseScope(input.Scope);
			query = query.Where(x => x.Scope == scope);
		}

		if (input.ActiveOn is { } activeOn)
		{
			var date = activeOn.Date;
			query = query.Where(x => x.StartDate <= date && x.EndDate >= date);
		}

		var rows = query.OrderByDescending(x => x.StartDate).ToList();
		return rows.Select(ToDto).ToList();
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.ManageExceptionWindow)]
	public async Task DeleteAsync(Guid id)
	{
		var entity = await repository.GetAsync(id);
		entity.EnsureDeletable(clock.Now);
		await repository.DeleteAsync(entity, autoSave: false);

		await financeAuditWriter.WriteAsync(
			entity.Scope == ExceptionWindowScope.AcademicYear ? entity.ScopeId : null,
			FinanceAuditEntityTypes.CollectionPolicyExceptionWindow,
			entity.Id,
			$"{entity.Scope} exception window ({entity.StartDate:yyyy-MM-dd} to {entity.EndDate:yyyy-MM-dd})",
			FinanceAuditActions.Delete,
			"Exception window deleted before it became active.",
			null,
			entity.Reason);

		await CurrentUnitOfWork!.SaveChangesAsync();
	}

	private static ExceptionWindowScope ParseScope(string value) => Enum.TryParse<ExceptionWindowScope>(value, true, out var scope)
		? scope
		: throw new BusinessException(CollectionPolicyErrorCodes.ExceptionWindowScopeIdRequired).WithData("scope", value);

	private static ExceptionWindowEffect ParseEffect(string value) => Enum.TryParse<ExceptionWindowEffect>(value, true, out var effect)
		? effect
		: throw new BusinessException(CollectionPolicyErrorCodes.InvalidLateFeeRule).WithData("field", "exceptionWindowEffect");

	private CollectionPolicyExceptionWindowDto ToDto(CollectionPolicyExceptionWindow entity) => new()
	{
		Id = entity.Id,
		Scope = entity.Scope.ToString(),
		ScopeId = entity.ScopeId,
		StartDate = entity.StartDate,
		EndDate = entity.EndDate,
		Effect = entity.Effect.ToString(),
		ExtensionDays = entity.ExtensionDays,
		ReasonCode = entity.ReasonCode,
		Reason = entity.Reason,
		CreatedByUserId = entity.CreatedByUserId,
		IsActive = clock.Now.Date >= entity.StartDate && clock.Now.Date <= entity.EndDate,
		CreationTime = entity.CreationTime
	};
}

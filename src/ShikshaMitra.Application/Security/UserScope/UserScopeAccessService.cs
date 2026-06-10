using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Security.UserScope;

[RemoteService(false)]
public class UserScopeAccessService(IUserScopeService userScopeService)
	: ApplicationService, IUserScopeAccessService, ITransientDependency
{
	public async Task<UserScopeContext> GetCurrentUserScopeAsync()
	{
		if (!CurrentUser.Id.HasValue)
		{
			throw new BusinessException(UserScopeErrorCodes.NoCurrentUser);
		}

		return await userScopeService.GetUserScopeAsync(CurrentUser.Id.Value);
	}

	public async Task<bool> CurrentUserHasTenantWideScopeAsync()
	{
		var scope = await GetCurrentUserScopeAsync();
		return scope.HasTenantWideScope();
	}

	public IQueryable<TEntity> ApplyScope<TEntity>(
		IQueryable<TEntity> query,
		UserScopeContext scope)
		where TEntity : IUserScopedEntity
	{
		return query.ApplyUserScope(scope);
	}

	public async Task<IQueryable<TEntity>> ApplyCurrentUserScopeAsync<TEntity>(
		IQueryable<TEntity> query)
		where TEntity : IUserScopedEntity
	{
		if (!CurrentUser.Id.HasValue)
		{
			return query.Where(_ => false);
		}

		var scope = await userScopeService.GetUserScopeAsync(CurrentUser.Id.Value);
		return ApplyScope(query, scope);
	}

	public void EnsureHasAnyAccess(
		UserScopeContext scope,
		string? errorCode = null)
	{
		if (scope.IsAdmin || scope.HasAnyAccess)
		{
			return;
		}

		LogDeniedAccess(scope, entityType: null, entityId: null, operation: "scope-any-access");
		throw new BusinessException(errorCode ?? UserScopeErrorCodes.AccessDenied);
	}

	public void EnsureCanAccess<TEntity>(
		TEntity entity,
		UserScopeContext scope,
		string? errorCode = null,
		string? operation = null)
		where TEntity : IUserScopedEntity
	{
		if (UserScopeQueryExtensions.HasAccessToEntity(entity, scope))
		{
			return;
		}

		LogDeniedAccess(
			scope,
			typeof(TEntity).Name,
			entity is IEntity<Guid> guidEntity ? guidEntity.Id : null,
			operation);

		throw new BusinessException(errorCode ?? UserScopeErrorCodes.AccessDenied);
	}

	public async Task EnsureCurrentUserCanAccessAsync<TEntity>(
		TEntity entity,
		string? errorCode = null,
		string? operation = null)
		where TEntity : IUserScopedEntity
	{
		var scope = await GetCurrentUserScopeAsync();
		EnsureCanAccess(entity, scope, errorCode, operation);
	}

	private void LogDeniedAccess(
		UserScopeContext scope,
		string? entityType,
		Guid? entityId,
		string? operation)
	{
		Logger.LogInformation(
			"UserScope denied direct access. UserId: {UserId}, TenantId: {TenantId}, EntityType: {EntityType}, EntityId: {EntityId}, Operation: {Operation}, IsAdmin: {IsAdmin}, HasAnyAccess: {HasAnyAccess}",
			scope.UserId,
			scope.TenantId,
			entityType,
			entityId,
			operation,
			scope.IsAdmin,
			scope.HasAnyAccess);
	}
}

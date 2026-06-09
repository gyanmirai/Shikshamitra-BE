using System.Linq;
using System.Threading.Tasks;

namespace ShikshaMitra.Security.UserScope;

public interface IUserScopeAccessService
{
	Task<UserScopeContext> GetCurrentUserScopeAsync();

	Task<bool> CurrentUserHasTenantWideScopeAsync();

	IQueryable<TEntity> ApplyScope<TEntity>(
		IQueryable<TEntity> query,
		UserScopeContext scope)
		where TEntity : IUserScopedEntity;

	Task<IQueryable<TEntity>> ApplyCurrentUserScopeAsync<TEntity>(
		IQueryable<TEntity> query)
		where TEntity : IUserScopedEntity;

	void EnsureHasAnyAccess(
		UserScopeContext scope,
		string? errorCode = null);

	void EnsureCanAccess<TEntity>(
		TEntity entity,
		UserScopeContext scope,
		string? errorCode = null,
		string? operation = null)
		where TEntity : IUserScopedEntity;

	Task EnsureCurrentUserCanAccessAsync<TEntity>(
		TEntity entity,
		string? errorCode = null,
		string? operation = null)
		where TEntity : IUserScopedEntity;
}

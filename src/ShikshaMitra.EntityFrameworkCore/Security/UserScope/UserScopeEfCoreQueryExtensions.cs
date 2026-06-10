using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Security.UserScope;

namespace ShikshaMitra.EntityFrameworkCore.Security.UserScope;

public static class UserScopeEfCoreQueryExtensions
{
	public static async Task<int> CountWithUserScopeAsync<TEntity>(
		this IQueryable<TEntity> queryable,
		UserScopeContext userScope,
		CancellationToken cancellationToken = default)
		where TEntity : IUserScopedEntity
	{
		return await queryable.ApplyUserScope(userScope).CountAsync(cancellationToken);
	}

	public static async Task<bool> AnyWithUserScopeAsync<TEntity>(
		this IQueryable<TEntity> queryable,
		UserScopeContext userScope,
		CancellationToken cancellationToken = default)
		where TEntity : IUserScopedEntity
	{
		return await queryable.ApplyUserScope(userScope).AnyAsync(cancellationToken);
	}
}

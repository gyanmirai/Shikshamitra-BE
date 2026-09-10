using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ShikshaMitra.Infrastructure.Data;

/// <summary>
/// PostgreSQL generates the integer identity in the owned tag's composite key.
/// SQLite only auto-generates single-column integer primary keys, so supply the
/// same unique identity behavior in the in-memory API fixture.
/// </summary>
public sealed class AccountCategoryTagIdentityInterceptor : SaveChangesInterceptor
{
	private int _nextId;

	public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
	{
		AssignIds(eventData.Context);
		return result;
	}

	public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
		InterceptionResult<int> result, CancellationToken cancellationToken = default)
	{
		AssignIds(eventData.Context);
		return ValueTask.FromResult(result);
	}

	private void AssignIds(DbContext? context)
	{
		if (context is null || !context.Database.IsSqlite()) return;
		foreach (var entry in context.ChangeTracker.Entries())
		{
			if (entry.State != EntityState.Added || entry.Metadata.GetTableName() != "AppIncomeCategoryTags") continue;
			var id = entry.Property("Id");
			if (!id.IsTemporary && (int)(id.CurrentValue ?? 0) != 0) continue;
			id.CurrentValue = Interlocked.Increment(ref _nextId);
			id.IsTemporary = false;
		}
	}
}

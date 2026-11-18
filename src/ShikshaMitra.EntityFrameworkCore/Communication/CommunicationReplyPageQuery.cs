using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Communication;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.EntityFrameworkCore.Communication;

[ExposeServices(typeof(ICommunicationReplyPageQuery))]
public sealed class CommunicationReplyPageQuery(IDbContextProvider<ShikshaMitraDbContext> contexts)
	: ICommunicationReplyPageQuery, ITransientDependency
{
	public async Task<IReadOnlyList<CommunicationReply>> GetPageAsync(IQueryable<CommunicationReply> filteredQuery, int skip, int take,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(filteredQuery);
		if (skip < 0) throw new ArgumentOutOfRangeException(nameof(skip));
		if (take is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(take));
		cancellationToken.ThrowIfCancellationRequested();
		if ((await contexts.GetDbContextAsync()).Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
		{
			// SQLite cannot order DateTimeOffset; the PostgreSQL path keeps native database paging.
			var filtered = await filteredQuery.ToListAsync(cancellationToken);
			return filtered.OrderByDescending(x => x.ReceivedAt).ThenByDescending(x => x.Id).Skip(skip).Take(take).ToArray();
		}
		return await filteredQuery.OrderByDescending(x => x.ReceivedAt).ThenByDescending(x => x.Id)
			.Skip(skip).Take(take).ToListAsync(cancellationToken);
	}
}

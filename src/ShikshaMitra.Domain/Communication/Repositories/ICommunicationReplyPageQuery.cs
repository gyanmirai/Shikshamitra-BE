using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.Communication;

public interface ICommunicationReplyPageQuery
{
	Task<IReadOnlyList<CommunicationReply>> GetPageAsync(IQueryable<CommunicationReply> filteredQuery, int skip, int take,
		CancellationToken cancellationToken = default);
}

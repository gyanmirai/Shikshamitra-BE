using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Library.IssueReturn;

namespace ShikshaMitra.Library.Members;

public partial class LibraryMemberAppService
{
	private Task<Dictionary<Guid, LibraryMemberStatisticsDto>> GetStatisticsMapAsync(IEnumerable<Guid> personIds) =>
		statisticsReader.ReadAsync(personIds);
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Rooms;

public enum RoomFeatureTextOperator { Contains, Equals, StartsWith, EndsWith }

public sealed record RoomFeatureTextFilter(RoomFeatureTextOperator Operator, string Value);

public interface IRoomFeatureQuery
{
	Task<IQueryable<Guid>> GetMatchingRoomIdsAsync(IReadOnlyList<RoomFeatureTextFilter> filters);
}

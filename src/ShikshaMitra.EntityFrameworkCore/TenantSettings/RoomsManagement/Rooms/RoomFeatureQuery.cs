using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Rooms;

public class RoomFeatureQuery(IDbContextProvider<ShikshaMitraDbContext> contexts) : IRoomFeatureQuery, ITransientDependency
{
	public async Task<IQueryable<Guid>> GetMatchingRoomIdsAsync(IReadOnlyList<RoomFeatureTextFilter> filters)
	{
		var db = await contexts.GetDbContextAsync();
		if (filters.Count == 0) return db.Set<Room>().Select(room => room.Id);
		var postgres = db.Database.IsNpgsql();
		var joined = postgres
			? "(SELECT string_agg(value, ', ' ORDER BY ordinal) FROM jsonb_array_elements_text(CASE WHEN jsonb_typeof(room.\"Features\") = 'array' THEN room.\"Features\" ELSE '[]'::jsonb END) WITH ORDINALITY AS feature(value, ordinal))"
			: "(SELECT group_concat(value, ', ') FROM json_each(COALESCE(room.\"Features\", '[]')))";
		var text = $"LOWER(COALESCE({joined}, ''))";
		var predicates = new List<string>();
		var values = new List<object>();
		foreach (var filter in filters)
		{
			var parameter = "{" + values.Count + "}";
			values.Add(filter.Value.ToLowerInvariant());
			predicates.Add(filter.Operator switch
			{
				RoomFeatureTextOperator.Equals => $"{text} = {parameter}",
				RoomFeatureTextOperator.Contains => postgres ? $"strpos({text}, {parameter}) > 0" : $"instr({text}, {parameter}) > 0",
				RoomFeatureTextOperator.StartsWith => $"substr({text}, 1, length({parameter})) = {parameter}",
				RoomFeatureTextOperator.EndsWith when filter.Value.Length == 0 => "1 = 1",
				RoomFeatureTextOperator.EndsWith => postgres
					? $"right({text}, length({parameter})) = {parameter}"
					: $"substr({text}, -length({parameter})) = {parameter}",
				_ => throw new ArgumentOutOfRangeException(nameof(filters))
			});
		}
		var sql = "SELECT room.* FROM \"academic_Rooms\" AS room WHERE " + string.Join(" OR ", predicates.Select(x => "(" + x + ")"));
		return db.Set<Room>().FromSqlRaw(sql, values.ToArray()).Select(room => room.Id);
	}
}

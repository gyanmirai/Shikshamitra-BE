using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Rooms.Services;

public static class RoomSeatingLayout
{
	public sealed record SeatPosition(int SeatNumber, int Row, int Column);

	public static int GetCapacity(Room room) => room.ColumnRows != null
		? room.ColumnRows.Sum() * Math.Max(0, room.BenchCapacity)
		: Math.Max(0, room.TotalBenches > 0 ? room.TotalBenches * room.BenchCapacity : room.Capacity);

	public static List<SeatPosition> GetSeats(Room room)
	{
		var capacity = GetCapacity(room);
		if (capacity == 0) return [];
		var benchCapacity = Math.Max(1, room.BenchCapacity);
		var seats = new List<SeatPosition>(capacity);
		if (room.ColumnRows is { Length: > 0 } columnRows)
		{
			for (var row = 0; row < columnRows.Max(); row++)
			{
				for (var column = 0; column < columnRows.Length; column++)
				{
					if (row >= columnRows[column]) continue;
					for (var position = 0; position < benchCapacity; position++)
						seats.Add(new SeatPosition(seats.Count + 1, row, column));
				}
			}
			return seats;
		}

		// Preserve row-wise seat numbers for rooms saved before ColumnRows existed.
		var columns = Math.Max(1, room.BenchesPerColumn);
		for (var index = 0; index < capacity; index++)
			seats.Add(new SeatPosition(index + 1, index / (columns * benchCapacity), index / benchCapacity % columns));
		return seats;
	}
}

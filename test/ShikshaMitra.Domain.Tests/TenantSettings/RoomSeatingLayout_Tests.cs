using System.Linq;
using Shouldly;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms.Services;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class RoomSeatingLayout_Tests
{
	[Fact]
	public void Unequal_columns_skip_nonexistent_benches_and_keep_row_wise_numbers()
	{
		var room = new Room();
		room.SetColumnLayout([3, 1, 2], 2);
		var seats = RoomSeatingLayout.GetSeats(room);
		seats.Count.ShouldBe(12);
		seats.Select(seat => seat.SeatNumber).ShouldBe(Enumerable.Range(1, 12));
		seats.Select(seat => (seat.Row, seat.Column)).ShouldBe(new[]
		{
			(0, 0), (0, 0), (0, 1), (0, 1), (0, 2), (0, 2),
			(1, 0), (1, 0), (1, 2), (1, 2), (2, 0), (2, 0)
		});
	}

	[Fact]
	public void Saved_column_rows_override_stale_rectangular_totals()
	{
		var room = new Room { ColumnRows = [1, 3], BenchCapacity = 2, Capacity = 999, TotalBenches = 999 };
		RoomSeatingLayout.GetCapacity(room).ShouldBe(8);
		RoomSeatingLayout.GetSeats(room).Count.ShouldBe(8);
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void Zero_layout_dimensions_do_not_generate_exam_seats_from_stale_totals(bool zeroColumns)
	{
		var room = new Room
		{
			ColumnRows = zeroColumns ? [] : [2, 3],
			BenchCapacity = zeroColumns ? 2 : 0,
			TotalBenches = 99,
			Capacity = 198
		};
		RoomSeatingLayout.GetCapacity(room).ShouldBe(0);
		RoomSeatingLayout.GetSeats(room).ShouldBeEmpty();
	}

	[Theory]
	[InlineData(6, 3, 2, 12)]
	[InlineData(5, 3, 2, 10)]
	[InlineData(0, 2, 1, 7)]
	public void Legacy_rectangular_and_partial_rooms_keep_every_existing_seat_number(int benches, int columns, int perBench, int capacity)
	{
		var room = new Room { TotalBenches = benches, BenchesPerColumn = columns, BenchCapacity = perBench, Capacity = capacity };
		var seats = RoomSeatingLayout.GetSeats(room);
		seats.Count.ShouldBe(capacity);
		for (var index = 0; index < seats.Count; index++)
		{
			seats[index].SeatNumber.ShouldBe(index + 1);
			seats[index].Row.ShouldBe(index / (columns * perBench));
			seats[index].Column.ShouldBe(index / perBench % columns);
		}
	}

	[Fact]
	public void Initial_column_counts_for_a_legacy_partial_grid_preserve_seat_positions()
	{
		var room = new Room { TotalBenches = 5, BenchesPerColumn = 3, BenchCapacity = 2 };
		var before = RoomSeatingLayout.GetSeats(room);
		room.SetColumnLayout([2, 2, 1], 2);
		RoomSeatingLayout.GetSeats(room).ShouldBe(before);
	}
}

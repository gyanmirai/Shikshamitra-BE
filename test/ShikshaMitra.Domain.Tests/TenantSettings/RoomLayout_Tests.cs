using System;
using System.Linq;
using Shouldly;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.TenantSettings;

public class RoomLayout_Tests
{
	[Fact]
	public void Unequal_columns_determine_benches_and_capacity()
	{
		var room = new Room { Capacity = 999, TotalBenches = 999 };
		var rows = new[] { 3, 5, 2 };
		room.SetColumnLayout(rows, 2);
		room.ColumnRows.ShouldBe(new[] { 3, 5, 2 });
		room.BenchesPerRow.ShouldBe(5);
		room.BenchesPerColumn.ShouldBe(3);
		room.TotalBenches.ShouldBe(10);
		room.Capacity.ShouldBe(20);
		rows[0] = 100;
		room.ColumnRows![0].ShouldBe(3);
	}

	[Theory]
	[InlineData(0, 2)]
	[InlineData(-1, 2)]
	[InlineData(101, 2)]
	[InlineData(3, -1)]
	[InlineData(3, 21)]
	public void Invalid_rows_or_bench_capacity_are_rejected(int rows, int capacity)
	{
		Should.Throw<BusinessException>(() => new Room().SetColumnLayout([rows], capacity));
	}

	[Fact]
	public void Excessive_column_counts_are_rejected()
	{
		Should.Throw<BusinessException>(() => new Room().SetColumnLayout(Enumerable.Repeat(1, 51).ToArray(), 2));
	}

	[Fact]
	public void Maximum_layout_dimensions_remain_valid()
	{
		var room = new Room();
		room.SetColumnLayout(Enumerable.Repeat(100, 50).ToArray(), 20);
		room.TotalBenches.ShouldBe(5000);
		room.Capacity.ShouldBe(100000);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(2)]
	public void Zero_columns_clear_the_existing_layout_and_totals(int capacity)
	{
		var room = new Room();
		room.SetColumnLayout([3, 5], 2);
		room.SetColumnLayout([], capacity);
		room.ColumnRows.ShouldBeEmpty();
		room.BenchesPerRow.ShouldBe(0);
		room.BenchesPerColumn.ShouldBe(0);
		room.BenchCapacity.ShouldBe(capacity);
		room.TotalBenches.ShouldBe(0);
		room.Capacity.ShouldBe(0);
	}

	[Fact]
	public void Zero_bench_capacity_clears_totals_and_retains_rows_for_restoring_capacity()
	{
		var room = new Room();
		room.SetColumnLayout([3, 5], 0);
		room.ColumnRows.ShouldBe(new[] { 3, 5 });
		room.BenchesPerRow.ShouldBe(5);
		room.BenchesPerColumn.ShouldBe(2);
		room.BenchCapacity.ShouldBe(0);
		room.TotalBenches.ShouldBe(0);
		room.Capacity.ShouldBe(0);
		room.SetColumnLayout(room.ColumnRows, 2);
		room.TotalBenches.ShouldBe(8);
		room.Capacity.ShouldBe(16);
	}

	[Fact]
	public void Legacy_clients_can_keep_existing_rectangular_totals()
	{
		var room = new Room { TotalBenches = 7, Capacity = 14 };
		room.SetColumnLayout(null, 2);
		room.TotalBenches.ShouldBe(7);
		room.Capacity.ShouldBe(14);
	}

	[Fact]
	public void Feature_names_are_trimmed_and_normalized_with_tenant_ownership()
	{
		var tenant = Guid.NewGuid();
		var feature = new RoomFeature(Guid.NewGuid(), tenant, "  Smart Board  ");
		feature.Name.ShouldBe("Smart Board");
		feature.NormalizedName.ShouldBe("SMART BOARD");
		feature.TenantId.ShouldBe(tenant);
		Should.Throw<ArgumentException>(() => new RoomFeature(Guid.NewGuid(), tenant, "  "));
	}
}

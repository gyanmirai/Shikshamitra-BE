using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Rooms;

public class Room : AuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public string RoomName { get; set; }
	[System.ComponentModel.DataAnnotations.Schema.NotMapped]
	public bool HasImage => ExtraProperties.TryGetValue("RoomImageBlob", out var image) && !string.IsNullOrEmpty(image?.ToString());
	public int Capacity { get; set; }
	public Guid BuildingId { get; set; }
	public Guid FloorId { get; set; }
	public Guid RoomTypeId { get; set; }

	// Benches Details
	public int TotalBenches { get; set; }
	public int BenchCapacity { get; set; }
	public int BenchesPerRow { get; set; }
	public int BenchesPerColumn { get; set; }
	public int[]? ColumnRows { get; set; }

	public void SetColumnLayout(int[]? columnRows, int benchCapacity)
	{
		if (columnRows == null) return;
		if (columnRows.Length > 50 || System.Linq.Enumerable.Any(columnRows, rows => rows is < 1 or > 100)
			|| benchCapacity is < 0 or > 20)
		{
			throw new Volo.Abp.BusinessException("Rooms:InvalidColumnLayout")
				.WithData("Details", "Use 0 to 50 columns, 1 to 100 rows per column, and 0 to 20 students per bench.");
		}
		ColumnRows = (int[])columnRows.Clone();
		BenchesPerColumn = columnRows.Length;
		BenchesPerRow = columnRows.Length == 0 ? 0 : System.Linq.Enumerable.Max(columnRows);
		TotalBenches = benchCapacity == 0 ? 0 : System.Linq.Enumerable.Sum(columnRows);
		BenchCapacity = benchCapacity;
		Capacity = TotalBenches * benchCapacity;
	}

	// Features (stored as JSON array)
	public List<string>? Features { get; set; }

	// ── Exam seating layout metadata (spec §15 room layout templates) ──
	// Board/entrance orientation shown on the seat canvas.
	public string? BoardPosition { get; set; }
	public string? EntrancePosition { get; set; }
	// Comma-separated seat numbers (1-based, matching ExamSeatAllocation.SeatNumber ordering).
	public string? DisabledSeatNumbersCsv { get; set; }
	public string? ReservedSeatNumbersCsv { get; set; }
	public string? AccessibleSeatNumbersCsv { get; set; }
	// Zero-based column indexes after which an aisle gap is rendered.
	public string? AisleAfterColumnsCsv { get; set; }
}

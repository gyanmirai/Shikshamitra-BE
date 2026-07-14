using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Rooms;

public class Room : AuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public string RoomName { get; set; }
	public int Capacity { get; set; }
	public Guid BuildingId { get; set; }
	public Guid FloorId { get; set; }
	public Guid RoomTypeId { get; set; }

	// Benches Details
	public int TotalBenches { get; set; }
	public int BenchCapacity { get; set; }
	public int BenchesPerRow { get; set; }
	public int BenchesPerColumn { get; set; }

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

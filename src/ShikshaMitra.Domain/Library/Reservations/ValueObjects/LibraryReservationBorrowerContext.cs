using System;

namespace ShikshaMitra.Library.Reservations;

public sealed record LibraryReservationBorrowerContext(Guid? BoardId, Guid? MediumId, Guid? StageId,
	Guid? StandardId, Guid? DivisionId, string? ScopeSnapshotJson = null);

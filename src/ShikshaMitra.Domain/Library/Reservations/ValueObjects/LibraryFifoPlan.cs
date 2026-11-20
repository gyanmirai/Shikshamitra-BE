using System;
using System.Collections.Generic;

namespace ShikshaMitra.Library.Reservations;

public sealed record LibraryReservationQueueEntry(Guid ReservationId, long? Sequence, LibraryReservationState State,
	LibraryReservationEligibility Eligibility, bool AtCheckoutLimit = false, Guid? PickupSectionId = null);

public sealed record LibraryReservationAvailableCopy(Guid CopyId, int CopyNumber, Guid? SectionId = null);

public sealed record LibraryReservationAllocation(Guid ReservationId, Guid CopyId);

public sealed record LibraryFifoPlan(IReadOnlyList<LibraryReservationAllocation> Allocations,
	IReadOnlyList<Guid> PermanentlyIneligibleRequests, bool BlockedByReview, IReadOnlySet<Guid>? BlockedSections = null)
{
	public bool BlocksAt(Guid sectionId) => BlockedByReview || BlockedSections?.Contains(sectionId) == true;
}

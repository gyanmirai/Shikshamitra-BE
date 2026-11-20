using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;

namespace ShikshaMitra.Library.Reservations;

public static class LibraryFifoAllocationPlanner
{
	public static LibraryFifoPlan Plan(IReadOnlyCollection<LibraryReservationQueueEntry> requests,
		IReadOnlyCollection<LibraryReservationAvailableCopy> copies)
	{
		if (requests.Select(row => row.ReservationId).Distinct().Count() != requests.Count ||
			copies.Select(row => row.CopyId).Distinct().Count() != copies.Count ||
			requests.Any(row => row.ReservationId == Guid.Empty || !Enum.IsDefined(row.State) || !Enum.IsDefined(row.Eligibility)) ||
			copies.Any(row => row.CopyId == Guid.Empty || row.CopyNumber <= 0))
			throw new BusinessException(LibraryReservationErrorCodes.InvalidQueue);
		var active = requests.Where(row => row.State is LibraryReservationState.Waiting or LibraryReservationState.Ready or LibraryReservationState.NeedsReview).ToList();
		var sequences = active.Where(row => row.Sequence.HasValue).Select(row => row.Sequence!.Value).ToList();
		if (sequences.Any(value => value <= 0) || sequences.Distinct().Count() != sequences.Count)
			throw new BusinessException(LibraryReservationErrorCodes.InvalidQueue);
		var available = copies.OrderBy(row => row.CopyNumber).ThenBy(row => row.CopyId).ToList();
		var allocations = new List<LibraryReservationAllocation>();
		var cancellations = new List<Guid>();
		var blockedSections = new HashSet<Guid>();
		foreach (var request in active.OrderBy(row => row.Sequence ?? long.MinValue).ThenBy(row => row.ReservationId))
		{
			if (request.State == LibraryReservationState.Ready && request.Eligibility != LibraryReservationEligibility.NeedsReview) continue;
			if (request.State == LibraryReservationState.NeedsReview || !request.Sequence.HasValue || request.Eligibility == LibraryReservationEligibility.NeedsReview)
			{
				if (request.PickupSectionId is not Guid section) return new(allocations, cancellations, true, blockedSections);
				blockedSections.Add(section);
				continue;
			}
			if (request.Eligibility == LibraryReservationEligibility.PermanentlyIneligible)
			{
				cancellations.Add(request.ReservationId);
				continue;
			}
			if (available.Count == 0) break;
			var index = available.FindIndex(copy => (!request.PickupSectionId.HasValue || copy.SectionId == request.PickupSectionId) &&
				(!copy.SectionId.HasValue || !blockedSections.Contains(copy.SectionId.Value)));
			if (index < 0) continue;
			allocations.Add(new(request.ReservationId, available[index].CopyId));
			available.RemoveAt(index);
		}
		return new(allocations, cancellations, false, blockedSections);
	}
}

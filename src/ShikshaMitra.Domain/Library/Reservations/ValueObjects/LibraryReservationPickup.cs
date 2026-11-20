using System;

namespace ShikshaMitra.Library.Reservations;

public sealed record LibraryReservationPickup(Guid SectionId, Guid CampusId, string Label);

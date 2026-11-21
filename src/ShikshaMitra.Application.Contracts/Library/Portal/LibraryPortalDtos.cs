using System;
using System.Collections.Generic;

namespace ShikshaMitra.Library.Portal;

public sealed record LibraryPortalStudent(Guid StudentId, string Name);
public sealed record LibraryPortalBook(Guid Id, string Title, string? Author, string? Isbn, int AvailableCopies);
public sealed record LibraryPortalLoan(Guid Id, string? Title, string Accession, DateOnly? IssuedOn, DateOnly? DueOn,
	DateOnly? ReturnedOn, bool IsOpen, int Renewals, bool CanPreviewRenewal);
public sealed record LibraryPortalReservation(Guid Id, string? Title, int State, DateOnly? RequestedOn, DateOnly? PickupBefore,
	string? PickupLocation, string Revision, bool CanCancel);
public sealed record LibraryPortalReserveInput(Guid BookId, Guid? PickupSectionId);
public sealed record LibraryPortalReservePreview(bool CanRequest, List<string> Reasons);
public sealed record LibraryPortalCancelInput(string Revision);
public sealed record LibraryPortalRenewPreview(bool CanRenew, DateOnly? DueOn, DateOnly? SchoolDate,
	string? Revision, List<string> Reasons);
public sealed record LibraryPortalRenewInput(DateOnly SchoolDate, string Revision, string IdempotencyKey);

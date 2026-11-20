using System;

namespace ShikshaMitra.Library.Portal;

public sealed record LibraryPortalReceiptYear(Guid Id, string Name);
public sealed record LibraryPortalReceiptDto(Guid Id, string Number, DateOnly ReceivedOn, string Kind, string VerificationStatus);

using System;
using Volo.Abp;

namespace ShikshaMitra.Library.Shared;

public sealed record LibraryOperationActor
{
	public LibraryActorKind Kind { get; }
	public Guid? UserId { get; }
	public LibrarySystemPurpose? SystemPurpose { get; }

	private LibraryOperationActor(LibraryActorKind kind, Guid? userId, LibrarySystemPurpose? purpose)
	{
		Kind = kind;
		UserId = userId;
		SystemPurpose = purpose;
	}

	public static LibraryOperationActor User(Guid userId)
	{
		if (userId == Guid.Empty) throw new BusinessException(LibraryErrorCodes.OperationActorInvalid,
			"A librarian action requires the actual user identity.");
		return new(LibraryActorKind.User, userId, null);
	}

	public static LibraryOperationActor System(LibrarySystemPurpose purpose)
	{
		if (!Enum.IsDefined(purpose)) throw new BusinessException(LibraryErrorCodes.OperationActorInvalid,
			"An automated Library action requires a recognized system purpose.");
		return new(LibraryActorKind.System, null, purpose);
	}
}

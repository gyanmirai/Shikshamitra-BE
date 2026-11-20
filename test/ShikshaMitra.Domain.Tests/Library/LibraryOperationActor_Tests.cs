using System;
using ShikshaMitra.Library.Shared;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryOperationActor_Tests
{
	[Fact]
	public void A_Librarian_Action_Should_Retain_The_Actual_User_Without_A_System_Attribution()
	{
		var userId = Guid.NewGuid();
		var actor = LibraryOperationActor.User(userId);
		actor.Kind.ShouldBe(LibraryActorKind.User);
		actor.UserId.ShouldBe(userId);
		actor.SystemPurpose.ShouldBeNull();
	}

	[Fact]
	public void An_Allocator_Action_Should_Name_The_System_Purpose_Without_Fabricating_A_User()
	{
		var actor = LibraryOperationActor.System(LibrarySystemPurpose.HoldAllocator);
		actor.Kind.ShouldBe(LibraryActorKind.System);
		actor.UserId.ShouldBeNull();
		actor.SystemPurpose.ShouldBe(LibrarySystemPurpose.HoldAllocator);
	}

	[Fact]
	public void A_Missing_Librarian_Identity_Should_Not_Be_Stored_As_An_Empty_User()
	{
		Should.Throw<BusinessException>(() => LibraryOperationActor.User(Guid.Empty)).Code
			.ShouldBe(LibraryErrorCodes.OperationActorInvalid);
	}

	[Fact]
	public void An_Unknown_System_Purpose_Should_Not_Create_An_Audit_Actor()
	{
		Should.Throw<BusinessException>(() => LibraryOperationActor.System((LibrarySystemPurpose)0)).Code
			.ShouldBe(LibraryErrorCodes.OperationActorInvalid);
	}
}

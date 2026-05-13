using ShikshaMitra.Security.UserScope;
using Shouldly;
using System;
using System.Collections.Generic;
using Xunit;

namespace ShikshaMitra.HR;

public class HrUserScopeAccess_Tests
{
	[Fact]
	public void CanAccessEmployeeStage_Should_Allow_TenantWide_Scope()
	{
		var scope = new UserScopeContext
		{
			UserId = Guid.NewGuid(),
			HasWildcardAcademicYear = true
		};

		HrUserScopeAccess.CanAccessEmployeeStage(scope, Guid.NewGuid()).ShouldBeTrue();
	}

	[Fact]
	public void CanAccessEmployeeStage_Should_Allow_Exact_Stage_Scope()
	{
		var stageId = Guid.NewGuid();
		var scope = new UserScopeContext
		{
			UserId = Guid.NewGuid(),
			MediumStageIds = new List<Guid> { stageId }
		};

		HrUserScopeAccess.CanAccessEmployeeStage(scope, stageId).ShouldBeTrue();
		HrUserScopeAccess.CanAccessEmployeeStage(scope, Guid.NewGuid()).ShouldBeFalse();
	}

	[Fact]
	public void CanAccessEmployeeStage_Should_Not_Treat_Board_Or_Medium_Wildcard_As_HrWide_When_Only_Stage_Is_Known()
	{
		var boardId = Guid.NewGuid();
		var mediumId = Guid.NewGuid();
		var scope = new UserScopeContext
		{
			UserId = Guid.NewGuid(),
			HasWildcardMedium = true,
			HasWildcardMediumStage = true,
			WildcardMediumBoardIds = new List<Guid> { boardId },
			WildcardMediumStageMediumIds = new List<Guid> { mediumId }
		};

		HrUserScopeAccess.HasHrWideAccess(scope).ShouldBeFalse();
		HrUserScopeAccess.CanAccessEmployeeStage(scope, Guid.NewGuid()).ShouldBeFalse();
	}

	[Fact]
	public void FilterEmployeeIdsByStage_Should_Not_Return_OutOfScope_Employees_For_Medium_Wildcard()
	{
		var inScopeStageId = Guid.NewGuid();
		var outOfScopeStageId = Guid.NewGuid();
		var inScopeEmployeeId = Guid.NewGuid();
		var outOfScopeEmployeeId = Guid.NewGuid();
		var unknownEmployeeId = Guid.NewGuid();
		var scope = new UserScopeContext
		{
			UserId = Guid.NewGuid(),
			MediumStageIds = new List<Guid> { inScopeStageId },
			HasWildcardMediumStage = true,
			WildcardMediumStageMediumIds = new List<Guid> { Guid.NewGuid() }
		};

		var result = HrUserScopeAccess.FilterEmployeeIdsByStage(
			new[] { inScopeEmployeeId, outOfScopeEmployeeId, unknownEmployeeId },
			new Dictionary<Guid, Guid?>
			{
				[inScopeEmployeeId] = inScopeStageId,
				[outOfScopeEmployeeId] = outOfScopeStageId,
				[unknownEmployeeId] = null
			},
			scope,
			includeUnknownStageEmployees: false);

		result.ShouldBe(new[] { inScopeEmployeeId });
	}
}

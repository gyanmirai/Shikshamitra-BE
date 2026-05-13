using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using ShikshaMitra.Security.UserScope;
using Xunit;

namespace ShikshaMitra.Domain.Tests.Security.UserScope;

/// <summary>
/// Tests to verify the NULL = ALL principle works correctly after bug fix.
/// </summary>
public class UserScopeNullEqualsAllTests
{
    [Fact]
    public void UserScopeContext_WithWildcardDivision_ShouldHaveWildcardFlag()
    {
        // Arrange & Act
        var scope = new UserScopeContext
        {
            UserId = Guid.NewGuid(),
            StandardIds = new List<Guid> { Guid.NewGuid() },
            HasWildcardDivision = true,  // NULL DivisionId in assignment
            DivisionIds = new List<Guid>() // Empty, but wildcard compensates
        };

        // Assert
        scope.HasWildcardDivision.ShouldBeTrue();
        scope.DivisionIds.ShouldBeEmpty();
        scope.HasAnyAccess.ShouldBeTrue(); // ✅ Should have access due to wildcard!
    }

    [Fact]
    public void UserScopeContext_WithAllWildcards_ShouldHaveAccess()
    {
        // Arrange & Act - God mode: all fields NULL
        var scope = new UserScopeContext
        {
            UserId = Guid.NewGuid(),
            HasWildcardAcademicYear = true,
            HasWildcardBoard = true,
            HasWildcardMedium = true,
            HasWildcardMediumStage = true,
            HasWildcardStandard = true,
            HasWildcardDivision = true
        };

        // Assert
        scope.HasAnyAccess.ShouldBeTrue(); // ✅ Should have god-mode access!
        scope.AcademicYearIds.ShouldBeEmpty();
        scope.BoardIds.ShouldBeEmpty();
        scope.MediumIds.ShouldBeEmpty();
        scope.StandardIds.ShouldBeEmpty();
        scope.DivisionIds.ShouldBeEmpty();
    }

    [Fact]
    public void UserScopeContext_WithNoWildcardsAndNoIds_ShouldNotHaveAccess()
    {
        // Arrange & Act - No assignments
        var scope = new UserScopeContext
        {
            UserId = Guid.NewGuid(),
            IsAdmin = false
        };

        // Assert
        scope.HasAnyAccess.ShouldBeFalse(); // ✅ Should have no access
    }

    [Fact]
    public void UserScopeContext_WithMixedSpecificAndWildcard_ShouldHaveAccess()
    {
        // Arrange & Act - Some specific, some wildcard
        var scope = new UserScopeContext
        {
            UserId = Guid.NewGuid(),
            BoardIds = new List<Guid> { Guid.NewGuid() }, // Specific board
            HasWildcardMedium = true,  // ALL mediums in that board
            StandardIds = new List<Guid> { Guid.NewGuid() }, // Specific standard
            DivisionIds = new List<Guid>() // No specific divisions
        };

        // Assert
        scope.HasAnyAccess.ShouldBeTrue(); // ✅ Should have access
        scope.BoardIds.ShouldNotBeEmpty();
        scope.HasWildcardMedium.ShouldBeTrue();
        scope.StandardIds.ShouldNotBeEmpty();
    }

    [Fact]
    public void UserScopeContext_AdminUser_ShouldAlwaysHaveAccess()
    {
        // Arrange & Act
        var scope = new UserScopeContext
        {
            UserId = Guid.NewGuid(),
            IsAdmin = true
            // No IDs, no wildcards
        };

        // Assert
        scope.HasAnyAccess.ShouldBeTrue(); // ✅ Admin always has access
    }

    [Fact]
    public void UserScopeContext_WithOnlySpecificIds_ShouldHaveAccess()
    {
        // Arrange & Act - Traditional specific assignment
        var scope = new UserScopeContext
        {
            UserId = Guid.NewGuid(),
            DivisionIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() }
        };

        // Assert
        scope.HasAnyAccess.ShouldBeTrue(); // ✅ Should have access to specific divisions
        scope.DivisionIds.Count.ShouldBe(2);
        scope.HasWildcardDivision.ShouldBeFalse();
    }

    [Fact]
    public void UserScopeAssignment_WithSpecificChildIds_ShouldNotGrantWildcardParentAccess()
    {
        // Arrange - mirrors HR bulk payload where board/medium are empty, but stage and standard are selected.
        var assignment = new UserScopeAssignment(
            id: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            academicYearId: Guid.NewGuid(),
            educationBoardId: null,
            educationMediumId: null,
            educationMediumStageId: Guid.NewGuid(),
            standardId: Guid.NewGuid(),
            divisionId: null);

        // Assert
        assignment.GrantsAllBoardsInAcademicYear().ShouldBeFalse();
        assignment.GrantsAllMediumsInBoard().ShouldBeFalse();
        assignment.GrantsAllMediumStagesInMedium().ShouldBeFalse();
        assignment.GrantsAllStandardsInMediumStage().ShouldBeFalse();
        assignment.GrantsAllDivisionsInStandard().ShouldBeTrue();
    }

    [Fact]
    public void UserScopeAssignment_WithAcademicYearOnly_ShouldGrantAllBoardsForThatYear()
    {
        // Arrange
        var assignment = new UserScopeAssignment(
            id: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            academicYearId: Guid.NewGuid());

        // Assert
        assignment.GrantsAllAcademicYears().ShouldBeFalse();
        assignment.GrantsAllBoardsInAcademicYear().ShouldBeTrue();
        assignment.GrantsAllMediumsInBoard().ShouldBeFalse();
    }

    [Fact]
    public void UserScopeAssignment_WithAllScopeIdsNull_ShouldGrantOrganizationWildcard()
    {
        // Arrange
        var assignment = new UserScopeAssignment(
            id: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            userId: Guid.NewGuid());

        // Assert
        assignment.GrantsAllAcademicYears().ShouldBeTrue();
        assignment.GrantsAllBoardsInAcademicYear().ShouldBeFalse();
    }

    [Fact]
    public void EffectiveScope_WithOrganizationAssignment_ShouldExpandAllWildcardFlags()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var assignment = new UserScopeAssignment(
            id: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            userId: userId);

        // Act
        var scope = UserScopeContext.CreateEffective(userId, assignment.TenantId, new[] { assignment });

        // Assert
        scope.HasWildcardAcademicYear.ShouldBeTrue();
        scope.HasWildcardBoard.ShouldBeTrue();
        scope.HasWildcardMedium.ShouldBeTrue();
        scope.HasWildcardMediumStage.ShouldBeTrue();
        scope.HasWildcardStandard.ShouldBeTrue();
        scope.HasWildcardDivision.ShouldBeTrue();
        scope.WildcardBoardAcademicYearIds.ShouldBeEmpty();
    }

    [Fact]
    public void EffectiveScope_WithAcademicYearAssignment_ShouldKeepYearConstraintAndExpandLowerWildcards()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var academicYearId = Guid.NewGuid();
        var assignment = new UserScopeAssignment(
            id: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            userId: userId,
            academicYearId: academicYearId);

        // Act
        var scope = UserScopeContext.CreateEffective(userId, assignment.TenantId, new[] { assignment });

        // Assert
        scope.AcademicYearIds.ShouldBe(new[] { academicYearId });
        scope.HasWildcardAcademicYear.ShouldBeFalse();
        scope.HasWildcardBoard.ShouldBeTrue();
        scope.HasWildcardMedium.ShouldBeTrue();
        scope.HasWildcardMediumStage.ShouldBeTrue();
        scope.HasWildcardStandard.ShouldBeTrue();
        scope.HasWildcardDivision.ShouldBeTrue();
        scope.WildcardBoardAcademicYearIds.ShouldBe(new[] { academicYearId });
        scope.HasTenantWideScope().ShouldBeFalse();
        scope.HasAcademicYearWideScope(academicYearId).ShouldBeTrue();
        scope.CanAccessMediumStage(Guid.NewGuid(), academicYearId: academicYearId).ShouldBeTrue();
    }

    [Fact]
    public void EffectiveScope_WithBoardAssignment_ShouldExpandBelowBoardOnly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var academicYearId = Guid.NewGuid();
        var boardId = Guid.NewGuid();
        var assignment = new UserScopeAssignment(
            id: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            userId: userId,
            academicYearId: academicYearId,
            educationBoardId: boardId);

        // Act
        var scope = UserScopeContext.CreateEffective(userId, assignment.TenantId, new[] { assignment });

        // Assert
        scope.AcademicYearIds.ShouldBe(new[] { academicYearId });
        scope.BoardIds.ShouldBe(new[] { boardId });
        scope.HasWildcardBoard.ShouldBeFalse();
        scope.HasWildcardMedium.ShouldBeTrue();
        scope.HasWildcardMediumStage.ShouldBeTrue();
        scope.HasWildcardStandard.ShouldBeTrue();
        scope.HasWildcardDivision.ShouldBeTrue();
        scope.WildcardMediumBoardIds.ShouldBe(new[] { boardId });
        scope.HasTenantWideScope().ShouldBeFalse();
        scope.CanAccessMediumStage(Guid.NewGuid()).ShouldBeFalse();
        scope.CanAccessMediumStage(Guid.NewGuid(), boardId: boardId).ShouldBeTrue();
    }

    [Fact]
    public void EffectiveScope_WithMediumAssignment_ShouldExpandBelowMediumOnly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var academicYearId = Guid.NewGuid();
        var boardId = Guid.NewGuid();
        var mediumId = Guid.NewGuid();
        var assignment = new UserScopeAssignment(
            id: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            userId: userId,
            academicYearId: academicYearId,
            educationBoardId: boardId,
            educationMediumId: mediumId);

        // Act
        var scope = UserScopeContext.CreateEffective(userId, assignment.TenantId, new[] { assignment });

        // Assert
        scope.BoardIds.ShouldBe(new[] { boardId });
        scope.MediumIds.ShouldBe(new[] { mediumId });
        scope.HasWildcardMedium.ShouldBeFalse();
        scope.HasWildcardMediumStage.ShouldBeTrue();
        scope.HasWildcardStandard.ShouldBeTrue();
        scope.HasWildcardDivision.ShouldBeTrue();
        scope.WildcardMediumStageMediumIds.ShouldBe(new[] { mediumId });
        scope.HasTenantWideScope().ShouldBeFalse();
        scope.CanAccessMediumStage(Guid.NewGuid()).ShouldBeFalse();
        scope.CanAccessMediumStage(Guid.NewGuid(), mediumId: mediumId).ShouldBeTrue();
    }

    [Fact]
    public void EffectiveScope_WithReportedHrPayloadShape_ShouldNotExpandParentWildcards()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var academicYearId = Guid.NewGuid();
        var mediumStageId = Guid.NewGuid();
        var standardId = Guid.NewGuid();
        var assignment = new UserScopeAssignment(
            id: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            userId: userId,
            academicYearId: academicYearId,
            educationBoardId: null,
            educationMediumId: null,
            educationMediumStageId: mediumStageId,
            standardId: standardId,
            divisionId: null);

        // Act
        var scope = UserScopeContext.CreateEffective(userId, assignment.TenantId, new[] { assignment });

        // Assert
        scope.AcademicYearIds.ShouldBe(new[] { academicYearId });
        scope.BoardIds.ShouldBeEmpty();
        scope.MediumIds.ShouldBeEmpty();
        scope.MediumStageIds.ShouldBe(new[] { mediumStageId });
        scope.StandardIds.ShouldBe(new[] { standardId });
        scope.HasWildcardBoard.ShouldBeFalse();
        scope.HasWildcardMedium.ShouldBeFalse();
        scope.HasWildcardMediumStage.ShouldBeFalse();
        scope.HasWildcardStandard.ShouldBeFalse();
        scope.HasWildcardDivision.ShouldBeTrue();
        scope.WildcardDivisionStandardIds.ShouldBe(new[] { standardId });
    }
}

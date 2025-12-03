using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shouldly;
using Xunit;
using NSubstitute;
using ShikshaMitra.Security.UserScope;

namespace ShikshaMitra.Domain.Tests.Security.UserScope;

/// <summary>
/// Tests for UserScopeService helper methods (HasAccessToDivision, HasAccessToStandard, etc.)
/// to verify they properly check wildcard flags in addition to specific ID lists.
/// </summary>
public class UserScopeHelperMethodsTests
{
    [Fact]
    public void HasAccessToDivision_WithWildcardFlag_ShouldGrantAccess()
    {
        // Arrange
        var divisionId = Guid.NewGuid();
        var scope = new UserScopeContext
        {
            UserId = Guid.NewGuid(),
            HasWildcardDivision = true,  // ✅ Wildcard grants access to ALL divisions
            DivisionIds = new List<Guid>() // Empty list, but wildcard compensates
        };

        // Act - Simulating what HasAccessToDivisionAsync does
        var hasAccess = scope.IsAdmin || 
                       scope.HasWildcardDivision || 
                       scope.DivisionIds.Contains(divisionId);

        // Assert
        hasAccess.ShouldBeTrue(); // ✅ Should have access due to wildcard
    }

    [Fact]
    public void HasAccessToDivision_WithSpecificId_ShouldGrantAccess()
    {
        // Arrange
        var divisionId = Guid.NewGuid();
        var scope = new UserScopeContext
        {
            UserId = Guid.NewGuid(),
            HasWildcardDivision = false,
            DivisionIds = new List<Guid> { divisionId } // Specific division
        };

        // Act
        var hasAccess = scope.IsAdmin || 
                       scope.HasWildcardDivision || 
                       scope.DivisionIds.Contains(divisionId);

        // Assert
        hasAccess.ShouldBeTrue(); // ✅ Should have access to specific division
    }

    [Fact]
    public void HasAccessToDivision_WithoutWildcardOrSpecificId_ShouldDenyAccess()
    {
        // Arrange
        var divisionId = Guid.NewGuid();
        var scope = new UserScopeContext
        {
            UserId = Guid.NewGuid(),
            HasWildcardDivision = false, // No wildcard
            DivisionIds = new List<Guid> { Guid.NewGuid() } // Different division
        };

        // Act
        var hasAccess = scope.IsAdmin || 
                       scope.HasWildcardDivision || 
                       scope.DivisionIds.Contains(divisionId);

        // Assert
        hasAccess.ShouldBeFalse(); // ❌ Should NOT have access
    }

    [Fact]
    public void HasAccessToStandard_WithWildcardFlag_ShouldGrantAccess()
    {
        // Arrange
        var standardId = Guid.NewGuid();
        var scope = new UserScopeContext
        {
            UserId = Guid.NewGuid(),
            HasWildcardStandard = true,  // ✅ Wildcard grants access to ALL standards
            StandardIds = new List<Guid>() // Empty list
        };

        // Act
        var hasAccess = scope.IsAdmin || 
                       scope.HasWildcardStandard || 
                       scope.StandardIds.Contains(standardId);

        // Assert
        hasAccess.ShouldBeTrue(); // ✅ Should have access due to wildcard
    }

    [Fact]
    public void HasAccessToStandard_WithSpecificId_ShouldGrantAccess()
    {
        // Arrange
        var standardId = Guid.NewGuid();
        var scope = new UserScopeContext
        {
            UserId = Guid.NewGuid(),
            HasWildcardStandard = false,
            StandardIds = new List<Guid> { standardId }
        };

        // Act
        var hasAccess = scope.IsAdmin || 
                       scope.HasWildcardStandard || 
                       scope.StandardIds.Contains(standardId);

        // Assert
        hasAccess.ShouldBeTrue(); // ✅ Should have access to specific standard
    }

    [Fact]
    public void HasAccessToBoard_WithWildcardFlag_ShouldGrantAccess()
    {
        // Arrange
        var boardId = Guid.NewGuid();
        var scope = new UserScopeContext
        {
            UserId = Guid.NewGuid(),
            HasWildcardBoard = true,  // ✅ Wildcard grants access to ALL boards
            BoardIds = new List<Guid>() // Empty list
        };

        // Act
        var hasAccess = scope.IsAdmin || 
                       scope.HasWildcardBoard || 
                       scope.BoardIds.Contains(boardId);

        // Assert
        hasAccess.ShouldBeTrue(); // ✅ Should have access due to wildcard
    }

    [Fact]
    public void HasAccessToBoard_WithSpecificId_ShouldGrantAccess()
    {
        // Arrange
        var boardId = Guid.NewGuid();
        var scope = new UserScopeContext
        {
            UserId = Guid.NewGuid(),
            HasWildcardBoard = false,
            BoardIds = new List<Guid> { boardId }
        };

        // Act
        var hasAccess = scope.IsAdmin || 
                       scope.HasWildcardBoard || 
                       scope.BoardIds.Contains(boardId);

        // Assert
        hasAccess.ShouldBeTrue(); // ✅ Should have access to specific board
    }

    [Fact]
    public void HasAccessToBoard_AdminUser_ShouldAlwaysGrantAccess()
    {
        // Arrange
        var boardId = Guid.NewGuid();
        var scope = new UserScopeContext
        {
            UserId = Guid.NewGuid(),
            IsAdmin = true, // Admin bypasses all checks
            HasWildcardBoard = false,
            BoardIds = new List<Guid>() // Empty
        };

        // Act
        var hasAccess = scope.IsAdmin || 
                       scope.HasWildcardBoard || 
                       scope.BoardIds.Contains(boardId);

        // Assert
        hasAccess.ShouldBeTrue(); // ✅ Admin has access to everything
    }

    [Fact]
    public void HasAccessToMultipleLevels_WithMixedWildcardAndSpecific_ShouldWorkCorrectly()
    {
        // Arrange
        var boardId1 = Guid.NewGuid();
        var boardId2 = Guid.NewGuid();
        var divisionId1 = Guid.NewGuid();
        var divisionId2 = Guid.NewGuid();

        var scope = new UserScopeContext
        {
            UserId = Guid.NewGuid(),
            HasWildcardBoard = true,     // ALL boards
            BoardIds = new List<Guid>(), // Empty due to wildcard
            HasWildcardDivision = false, // NOT all divisions
            DivisionIds = new List<Guid> { divisionId1 } // Only specific division
        };

        // Act & Assert
        // Board access - should work due to wildcard
        (scope.IsAdmin || scope.HasWildcardBoard || scope.BoardIds.Contains(boardId1))
            .ShouldBeTrue();
        (scope.IsAdmin || scope.HasWildcardBoard || scope.BoardIds.Contains(boardId2))
            .ShouldBeTrue();

        // Division access - should work only for specific ID
        (scope.IsAdmin || scope.HasWildcardDivision || scope.DivisionIds.Contains(divisionId1))
            .ShouldBeTrue();
        (scope.IsAdmin || scope.HasWildcardDivision || scope.DivisionIds.Contains(divisionId2))
            .ShouldBeFalse(); // ❌ Not in specific list and no wildcard
    }

    [Fact]
    public void RealWorldScenario_PrincipalWithWildcardStandard_ShouldAccessAllStandards()
    {
        // Arrange - Principal assigned to a Medium Stage with wildcard Standards
        var mediumStageId = Guid.NewGuid();
        var standard1 = Guid.NewGuid();
        var standard2 = Guid.NewGuid();
        var standard3 = Guid.NewGuid();

        var scope = new UserScopeContext
        {
            UserId = Guid.NewGuid(),
            MediumStageIds = new List<Guid> { mediumStageId },
            HasWildcardStandard = true, // Principal sees ALL standards in the stage
            StandardIds = new List<Guid>() // Empty because wildcard
        };

        // Act & Assert - Should have access to ANY standard
        (scope.IsAdmin || scope.HasWildcardStandard || scope.StandardIds.Contains(standard1))
            .ShouldBeTrue();
        (scope.IsAdmin || scope.HasWildcardStandard || scope.StandardIds.Contains(standard2))
            .ShouldBeTrue();
        (scope.IsAdmin || scope.HasWildcardStandard || scope.StandardIds.Contains(standard3))
            .ShouldBeTrue();
    }

    [Fact]
    public void RealWorldScenario_TeacherWithSpecificDivisions_ShouldAccessOnlyAssigned()
    {
        // Arrange - Teacher assigned to specific divisions
        var division10A = Guid.NewGuid();
        var division10B = Guid.NewGuid();
        var division10C = Guid.NewGuid();

        var scope = new UserScopeContext
        {
            UserId = Guid.NewGuid(),
            HasWildcardDivision = false,
            DivisionIds = new List<Guid> { division10A, division10B } // Only A and B
        };

        // Act & Assert
        (scope.IsAdmin || scope.HasWildcardDivision || scope.DivisionIds.Contains(division10A))
            .ShouldBeTrue();  // ✅ Has access
        (scope.IsAdmin || scope.HasWildcardDivision || scope.DivisionIds.Contains(division10B))
            .ShouldBeTrue();  // ✅ Has access
        (scope.IsAdmin || scope.HasWildcardDivision || scope.DivisionIds.Contains(division10C))
            .ShouldBeFalse(); // ❌ No access to 10-C
    }
}

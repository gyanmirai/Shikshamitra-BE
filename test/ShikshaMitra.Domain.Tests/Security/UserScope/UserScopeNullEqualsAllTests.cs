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
}

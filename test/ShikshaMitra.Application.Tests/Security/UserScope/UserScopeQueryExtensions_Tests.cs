using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.EntityFrameworkCore.Security.UserScope;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Security.UserScope;

public class UserScopeQueryExtensions_Tests
{
    [Fact]
    public void ApplyUserScope_Should_Allow_All_Rows_For_Organization_Scope()
    {
        var userId = Guid.NewGuid();
        var rows = CreateScopedRows();
        var scope = UserScopeContext.CreateEffective(
            userId,
            Guid.NewGuid(),
            new[]
            {
                new UserScopeAssignment(Guid.NewGuid(), Guid.NewGuid(), userId)
            });

        var result = rows.AsQueryable().ApplyUserScope(scope).ToList();

        result.Count.ShouldBe(rows.Count);
    }

    [Fact]
    public void ApplyUserScope_Should_Keep_AcademicYear_Constraint_For_AcademicYear_Scope()
    {
        var userId = Guid.NewGuid();
        var targetYearId = Guid.NewGuid();
        var otherYearId = Guid.NewGuid();
        var rows = new List<TestScopedEntity>
        {
            new() { Id = Guid.NewGuid(), AcademicYearId = targetYearId, EducationBoardId = Guid.NewGuid() },
            new() { Id = Guid.NewGuid(), AcademicYearId = otherYearId, EducationBoardId = Guid.NewGuid() }
        };
        var scope = UserScopeContext.CreateEffective(
            userId,
            Guid.NewGuid(),
            new[]
            {
                new UserScopeAssignment(Guid.NewGuid(), Guid.NewGuid(), userId, academicYearId: targetYearId)
            });

        var result = rows.AsQueryable().ApplyUserScope(scope).ToList();

        result.Count.ShouldBe(1);
        result[0].AcademicYearId.ShouldBe(targetYearId);
        scope.HasWildcardMediumStage.ShouldBeTrue();
    }

    [Fact]
    public void ApplyUserScope_Should_Allow_Stage_Only_Row_For_AcademicYear_Scope()
    {
        var userId = Guid.NewGuid();
        var targetYearId = Guid.NewGuid();
        var otherYearId = Guid.NewGuid();
        var rows = new List<TestScopedEntity>
        {
            new() { Id = Guid.NewGuid(), AcademicYearId = targetYearId, EducationMediumStageId = Guid.NewGuid() },
            new() { Id = Guid.NewGuid(), AcademicYearId = otherYearId, EducationMediumStageId = Guid.NewGuid() }
        };
        var scope = UserScopeContext.CreateEffective(
            userId,
            Guid.NewGuid(),
            new[]
            {
                new UserScopeAssignment(Guid.NewGuid(), Guid.NewGuid(), userId, academicYearId: targetYearId)
            });

        var result = rows.AsQueryable().ApplyUserScope(scope).ToList();

        result.Count.ShouldBe(1);
        result[0].AcademicYearId.ShouldBe(targetYearId);
        UserScopeQueryExtensions.HasAccessToEntity(result[0], scope).ShouldBeTrue();
    }

    [Fact]
    public void ApplyUserScope_Should_Not_Broaden_All_Boards_Across_Mixed_Assignment_Years()
    {
        var userId = Guid.NewGuid();
        var wideYearId = Guid.NewGuid();
        var boardOnlyYearId = Guid.NewGuid();
        var targetBoardId = Guid.NewGuid();
        var otherBoardId = Guid.NewGuid();
        var rows = new List<TestScopedEntity>
        {
            new() { Id = Guid.NewGuid(), AcademicYearId = wideYearId, EducationMediumStageId = Guid.NewGuid() },
            new() { Id = Guid.NewGuid(), AcademicYearId = boardOnlyYearId, EducationBoardId = targetBoardId },
            new() { Id = Guid.NewGuid(), AcademicYearId = boardOnlyYearId, EducationBoardId = otherBoardId }
        };
        var scope = UserScopeContext.CreateEffective(
            userId,
            Guid.NewGuid(),
            new[]
            {
                new UserScopeAssignment(Guid.NewGuid(), Guid.NewGuid(), userId, academicYearId: wideYearId),
                new UserScopeAssignment(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    userId,
                    academicYearId: boardOnlyYearId,
                    educationBoardId: targetBoardId)
            });

        var result = rows.AsQueryable().ApplyUserScope(scope).ToList();

        result.Count.ShouldBe(2);
        result.ShouldContain(row => row.AcademicYearId == wideYearId);
        result.ShouldContain(row => row.EducationBoardId == targetBoardId);
        result.ShouldNotContain(row => row.EducationBoardId == otherBoardId);
        UserScopeQueryExtensions.HasAccessToEntity(rows[2], scope).ShouldBeFalse();
    }

    [Fact]
    public void ApplyUserScope_Should_Filter_To_Board_For_Board_Scope()
    {
        var userId = Guid.NewGuid();
        var academicYearId = Guid.NewGuid();
        var targetBoardId = Guid.NewGuid();
        var otherBoardId = Guid.NewGuid();
        var rows = new List<TestScopedEntity>
        {
            new() { Id = Guid.NewGuid(), AcademicYearId = academicYearId, EducationBoardId = targetBoardId, EducationMediumId = Guid.NewGuid() },
            new() { Id = Guid.NewGuid(), AcademicYearId = academicYearId, EducationBoardId = otherBoardId, EducationMediumId = Guid.NewGuid() }
        };
        var scope = UserScopeContext.CreateEffective(
            userId,
            Guid.NewGuid(),
            new[]
            {
                new UserScopeAssignment(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    userId,
                    academicYearId: academicYearId,
                    educationBoardId: targetBoardId)
            });

        var result = rows.AsQueryable().ApplyUserScope(scope).ToList();

        result.Count.ShouldBe(1);
        result[0].EducationBoardId.ShouldBe(targetBoardId);
        scope.HasWildcardBoard.ShouldBeFalse();
        scope.HasWildcardMediumStage.ShouldBeTrue();
    }

    [Fact]
    public void ApplyUserScope_Should_Allow_Stage_Row_With_Board_For_Board_Scope()
    {
        var userId = Guid.NewGuid();
        var academicYearId = Guid.NewGuid();
        var targetBoardId = Guid.NewGuid();
        var otherBoardId = Guid.NewGuid();
        var rows = new List<TestScopedEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                AcademicYearId = academicYearId,
                EducationBoardId = targetBoardId,
                EducationMediumStageId = Guid.NewGuid()
            },
            new()
            {
                Id = Guid.NewGuid(),
                AcademicYearId = academicYearId,
                EducationBoardId = otherBoardId,
                EducationMediumStageId = Guid.NewGuid()
            },
            new()
            {
                Id = Guid.NewGuid(),
                AcademicYearId = academicYearId,
                EducationMediumStageId = Guid.NewGuid()
            }
        };
        var scope = UserScopeContext.CreateEffective(
            userId,
            Guid.NewGuid(),
            new[]
            {
                new UserScopeAssignment(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    userId,
                    academicYearId: academicYearId,
                    educationBoardId: targetBoardId)
            });

        var result = rows.AsQueryable().ApplyUserScope(scope).ToList();

        result.Count.ShouldBe(1);
        result[0].EducationBoardId.ShouldBe(targetBoardId);
        UserScopeQueryExtensions.HasAccessToEntity(result[0], scope).ShouldBeTrue();
        UserScopeQueryExtensions.HasAccessToEntity(rows[2], scope).ShouldBeFalse();
    }

    [Fact]
    public void ApplyUserScope_Should_Allow_Standard_Row_With_Medium_For_Medium_Scope()
    {
        var userId = Guid.NewGuid();
        var academicYearId = Guid.NewGuid();
        var targetMediumId = Guid.NewGuid();
        var otherMediumId = Guid.NewGuid();
        var rows = new List<TestScopedEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                AcademicYearId = academicYearId,
                EducationMediumId = targetMediumId,
                StandardId = Guid.NewGuid()
            },
            new()
            {
                Id = Guid.NewGuid(),
                AcademicYearId = academicYearId,
                EducationMediumId = otherMediumId,
                StandardId = Guid.NewGuid()
            },
            new()
            {
                Id = Guid.NewGuid(),
                AcademicYearId = academicYearId,
                StandardId = Guid.NewGuid()
            }
        };
        var scope = UserScopeContext.CreateEffective(
            userId,
            Guid.NewGuid(),
            new[]
            {
                new UserScopeAssignment(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    userId,
                    academicYearId: academicYearId,
                    educationMediumId: targetMediumId)
            });

        var result = rows.AsQueryable().ApplyUserScope(scope).ToList();

        result.Count.ShouldBe(1);
        result[0].EducationMediumId.ShouldBe(targetMediumId);
        UserScopeQueryExtensions.HasAccessToEntity(result[0], scope).ShouldBeTrue();
        UserScopeQueryExtensions.HasAccessToEntity(rows[2], scope).ShouldBeFalse();
    }

    [Fact]
    public void ApplyUserScope_Should_Not_Grant_Parent_Access_For_Reported_Hr_Payload_Shape()
    {
        var userId = Guid.NewGuid();
        var academicYearId = Guid.NewGuid();
        var targetStageId = Guid.NewGuid();
        var otherStageId = Guid.NewGuid();
        var standardId = Guid.NewGuid();
        var rows = new List<TestScopedEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                AcademicYearId = academicYearId,
                EducationMediumStageId = targetStageId,
                StandardId = standardId
            },
            new()
            {
                Id = Guid.NewGuid(),
                AcademicYearId = academicYearId,
                EducationMediumStageId = otherStageId,
                StandardId = Guid.NewGuid()
            }
        };
        var scope = UserScopeContext.CreateEffective(
            userId,
            Guid.NewGuid(),
            new[]
            {
                new UserScopeAssignment(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    userId,
                    academicYearId: academicYearId,
                    educationMediumStageId: targetStageId,
                    standardId: standardId)
            });

        var result = rows.AsQueryable().ApplyUserScope(scope).ToList();

        result.Count.ShouldBe(1);
        result[0].EducationMediumStageId.ShouldBe(targetStageId);
        scope.HasWildcardBoard.ShouldBeFalse();
        scope.HasWildcardMedium.ShouldBeFalse();
        scope.HasWildcardMediumStage.ShouldBeFalse();
    }

    private static List<TestScopedEntity> CreateScopedRows() =>
        new()
        {
            new() { Id = Guid.NewGuid(), AcademicYearId = Guid.NewGuid(), EducationBoardId = Guid.NewGuid() },
            new()
            {
                Id = Guid.NewGuid(),
                AcademicYearId = Guid.NewGuid(),
                EducationBoardId = Guid.NewGuid(),
                EducationMediumId = Guid.NewGuid(),
                EducationMediumStageId = Guid.NewGuid()
            }
        };

    private sealed class TestScopedEntity : IUserScopedEntity
    {
        public Guid Id { get; init; }
        public Guid? AcademicYearId { get; init; }
        public Guid? EducationBoardId { get; init; }
        public Guid? EducationMediumId { get; init; }
        public Guid? EducationMediumStageId { get; init; }
        public Guid? StandardId { get; init; }
        public Guid? DivisionId { get; init; }
    }
}

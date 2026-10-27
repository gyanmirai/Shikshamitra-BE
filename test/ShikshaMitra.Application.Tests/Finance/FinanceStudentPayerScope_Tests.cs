using System;
using System.Linq;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance;

public class FinanceStudentPayerScope_Tests
{
    [Fact]
    public void A_class_assignment_with_ancestor_ids_must_not_authorize_other_classes()
    {
        var tenant = Guid.NewGuid(); var year = Guid.NewGuid(); var board = Guid.NewGuid();
        var medium = Guid.NewGuid(); var stage = Guid.NewGuid(); var firstClass = Guid.NewGuid();
        var students = new[] { StudentFor(firstClass), StudentFor(Guid.NewGuid()) };
        var scope = UserScopeContext.CreateEffective(Guid.NewGuid(), tenant,
            [new UserScopeAssignment(Guid.NewGuid(), tenant, userId: Guid.NewGuid(), academicYearId: year,
                educationBoardId: board, educationMediumId: medium, educationMediumStageId: stage, standardId: firstClass)]);
        FinanceStudentScopeQueryBuilder.ApplyPayerScope(students.AsQueryable(), scope)
            .Select(student => student.Id).ToArray().ShouldBe([students[0].Id]);
        students.Select(student => StudentRecordScope.HasAccess(student, scope)).ToArray().ShouldBe([true, false]);

        Student StudentFor(Guid standard) => new(Guid.NewGuid(), tenant, "Scope", null, "Student",
            year, board, medium, stage, standard, null);
    }

    [Fact]
    public void An_explicit_stage_wildcard_allows_its_classes_but_not_another_stage_or_year()
    {
        var tenant = Guid.NewGuid(); var year = Guid.NewGuid(); var board = Guid.NewGuid();
        var medium = Guid.NewGuid(); var stage = Guid.NewGuid();
        var students = new[] { StudentFor(year, stage), StudentFor(year, Guid.NewGuid()), StudentFor(Guid.NewGuid(), stage) };
        var scope = UserScopeContext.CreateEffective(Guid.NewGuid(), tenant,
            [new UserScopeAssignment(Guid.NewGuid(), tenant, userId: Guid.NewGuid(), academicYearId: year,
                educationBoardId: board, educationMediumId: medium, educationMediumStageId: stage)]);
        FinanceStudentScopeQueryBuilder.ApplyPayerScope(students.AsQueryable(), scope)
            .Select(student => student.Id).ToArray().ShouldBe([students[0].Id]);
        students.Select(student => StudentRecordScope.HasAccess(student, scope)).ToArray().ShouldBe([true, false, false]);

        Student StudentFor(Guid schoolYear, Guid schoolStage) => new(Guid.NewGuid(), tenant, "Scope", null, "Student",
            schoolYear, board, medium, schoolStage, Guid.NewGuid(), null);
    }
}

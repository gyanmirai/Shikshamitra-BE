using System;
using System.Linq;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Application.Tests.Students.Records;

public class StudentRecordScope_Tests
{
	[Fact]
	public void Complete_division_assignment_must_not_authorize_sibling_or_unallocated_placements()
	{
		var year = Guid.NewGuid(); var board = Guid.NewGuid(); var medium = Guid.NewGuid();
		var stage = Guid.NewGuid(); var standard = Guid.NewGuid(); var division = Guid.NewGuid();
		var scope = new UserScopeContext { AcademicYearIds = [year], BoardIds = [board],
			MediumIds = [medium], MediumStageIds = [stage], StandardIds = [standard], DivisionIds = [division] };
		var assigned = new Placement(year, board, medium, stage, standard, division);
		StudentRecordScope.HasAccess(assigned, scope).ShouldBeTrue();
		StudentRecordScope.HasAccess(assigned with { DivisionId = Guid.NewGuid() }, scope).ShouldBeFalse();
		StudentRecordScope.HasAccess(assigned with { DivisionId = null }, scope).ShouldBeFalse();
		StudentRecordScope.HasAccess(assigned with { AcademicYearId = Guid.NewGuid() }, scope).ShouldBeFalse();
		scope.WildcardDivisionStandardIds.Add(standard);
		StudentRecordScope.HasAccess(assigned with { DivisionId = null }, scope).ShouldBeTrue();
	}

	private sealed record Placement(Guid? AcademicYearId, Guid? EducationBoardId, Guid? EducationMediumId,
		Guid? EducationMediumStageId, Guid? StandardId, Guid? DivisionId) : IUserScopedEntity;

	[Fact]
	public void Query_scope_should_not_treat_a_narrow_year_assignment_as_year_wide_access()
	{
		var year = Guid.NewGuid();
		var division = Guid.NewGuid();
		var board = Guid.NewGuid(); var medium = Guid.NewGuid();
		var stage = Guid.NewGuid(); var standard = Guid.NewGuid();
		var student = new Student(Guid.NewGuid(), Guid.NewGuid(), "Student", null, null, year, board, medium, stage, standard);
		student.UpdateAcademicPlacement(year, board, medium, stage, standard, division);
		var scope = new UserScopeContext { AcademicYearIds = [year] };

		StudentScopeAccessService.ApplyScope(new[] { student }.AsQueryable(), scope).ShouldBeEmpty();

		scope.DivisionIds.Add(division);
		StudentScopeAccessService.ApplyScope(new[] { student }.AsQueryable(), scope).Single().ShouldBe(student);
	}

	[Theory]
	[InlineData("year")]
	[InlineData("board")]
	[InlineData("medium")]
	[InlineData("stage")]
	[InlineData("standard")]
	[InlineData("division")]
	public void Explicit_wildcards_allow_descendants_only_in_the_assigned_year(string grant)
	{
		var year = Guid.NewGuid(); var board = Guid.NewGuid(); var medium = Guid.NewGuid();
		var stage = Guid.NewGuid(); var standard = Guid.NewGuid(); var division = Guid.NewGuid();
		var student = new Student(Guid.NewGuid(), Guid.NewGuid(), "Student", null, null, year, board, medium, stage, standard);
		student.UpdateAcademicPlacement(year, board, medium, stage, standard, division);
		var scope = new UserScopeContext { AcademicYearIds = [year], BoardIds = [board], MediumIds = [medium], MediumStageIds = [stage], StandardIds = [standard] };
		StudentRecordScope.HasAccess(student, scope).ShouldBeFalse();
		switch (grant)
		{
			case "year": scope.WildcardBoardAcademicYearIds.Add(year); break;
			case "board": scope.WildcardMediumBoardIds.Add(board); break;
			case "medium": scope.WildcardMediumStageMediumIds.Add(medium); break;
			case "stage": scope.WildcardStandardMediumStageIds.Add(stage); break;
			case "standard": scope.WildcardDivisionStandardIds.Add(standard); break;
			case "division": scope.DivisionIds.Add(division); break;
		}
		StudentRecordScope.HasAccess(student, scope).ShouldBeTrue();
		student.UpdateAcademicPlacement(Guid.NewGuid(), board, medium, stage, standard, division);
		StudentRecordScope.HasAccess(student, scope).ShouldBeFalse();
		scope.HasWildcardAcademicYear = true;
		StudentRecordScope.HasAccess(student, scope).ShouldBeTrue();
	}
}

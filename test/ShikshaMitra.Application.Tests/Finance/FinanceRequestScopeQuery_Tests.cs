using System;
using System.Linq;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.Students;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance;

public class FinanceRequestScopeQuery_Tests
{
	[Fact]
	public void Teacher_assignment_intersects_broad_student_scope_at_exact_division()
	{
		var year = Guid.NewGuid();
		var board = Guid.NewGuid();
		var medium = Guid.NewGuid();
		var stage = Guid.NewGuid();
		var standard = Guid.NewGuid();
		var assigned = Guid.NewGuid();
		var other = Guid.NewGuid();
		var teacher = Guid.NewGuid();
		var students = new[]
		{
			new Student(Guid.NewGuid(), null, "In", null, "Scope", year, board, medium, stage, standard, assigned),
			new Student(Guid.NewGuid(), null, "Out", null, "Scope", year, board, medium, stage, standard, other)
		}.AsQueryable();
		var assignments = new[]
		{
			new ClassTeacher(Guid.NewGuid(), null, standard, assigned, teacher, medium,
				year, board, medium, stage)
		}.AsQueryable();

		var result = FinanceStudentScopeQueryBuilder.ApplyExactTeacherAssignments(students, assignments, teacher).ToArray();

		result.Select(student => student.DivisionId).ShouldBe(new Guid?[] { assigned });
	}

	[Fact]
	public void Incomplete_or_draft_assignment_grants_no_student()
	{
		var year = Guid.NewGuid();
		var board = Guid.NewGuid();
		var medium = Guid.NewGuid();
		var stage = Guid.NewGuid();
		var standard = Guid.NewGuid();
		var division = Guid.NewGuid();
		var teacher = Guid.NewGuid();
		var student = new Student(Guid.NewGuid(), null, "In", null, "Scope", year, board, medium, stage, standard, division);
		var assignments = new[]
		{
			new ClassTeacher(Guid.NewGuid(), null, standard, division, teacher, medium),
			new ClassTeacher(Guid.NewGuid(), null, standard, division, teacher, medium,
				year, board, medium, stage, ClassTeacherAssignmentStatus.Draft)
		}.AsQueryable();

		FinanceStudentScopeQueryBuilder.ApplyExactTeacherAssignments(new[] { student }.AsQueryable(), assignments, teacher)
			.ShouldBeEmpty();
	}

	[Fact]
	public void Class_teacher_account_query_excludes_unlinked_and_other_student_accounts()
	{
		var allowedStudent = Guid.NewGuid();
		var deniedStudent = Guid.NewGuid();
		var accounts = new[]
		{
			Account(allowedStudent), Account(deniedStudent), Account(null)
		}.AsQueryable();

		var visible = FinanceFeeAccountScopeQueryBuilder.ApplyLinkedStudentCap(
			accounts, new[] { allowedStudent }.AsQueryable()).ToArray();

		visible.Select(account => account.StudentId).ShouldBe(new Guid?[] { allowedStudent });

		static FinanceFeeAccount Account(Guid? studentId) => new(Guid.NewGuid(), null,
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
			Guid.NewGuid(), Guid.NewGuid(), "Applicant", studentId: studentId);
	}
}

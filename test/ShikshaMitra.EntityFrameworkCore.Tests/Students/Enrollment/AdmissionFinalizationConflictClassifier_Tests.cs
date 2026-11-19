using System;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;
using Shouldly;
using Volo.Abp.Data;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionFinalizationConflictClassifier_Tests
{
	private readonly AdmissionFinalizationConflictClassifier _classifier = new();

	[Fact]
	public void Only_student_primary_key_unique_violation_is_an_identity_conflict()
	{
		_classifier.IsStudentIdentityConflict(PostgresFailure("23505", "PK_AppStudents")).ShouldBeTrue();
		_classifier.IsStudentIdentityConflict(new Exception("wrapper", PostgresFailure("23505", "PK_AppStudents"))).ShouldBeTrue();
		_classifier.IsStudentIdentityConflict(PostgresFailure("23503", "PK_AppStudents")).ShouldBeFalse();
		_classifier.IsStudentIdentityConflict(PostgresFailure("23505", "PK_AppAdmissionApplicants")).ShouldBeFalse();
		_classifier.IsStudentIdentityConflict(new InvalidOperationException("23505 PK_AppStudents")).ShouldBeFalse();
	}

	[Theory]
	[InlineData("IX_AppStudentOnboardingCases_TenantId_ApplicationId")]
	[InlineData("IX_AppStudentOnboardingCases_TenantId_IdempotencyKey")]
	[InlineData("IX_AppStudentOnboardingCases_TenantId_StudentId_AcademicYearId")]
	public void Relevant_onboarding_unique_conflicts_are_retryable(string constraint)
	{
		_classifier.IsConcurrentFinalization(PostgresFailure("23505", constraint)).ShouldBeTrue();
		_classifier.IsStudentIdentityConflict(PostgresFailure("23505", constraint)).ShouldBeFalse();
		_classifier.IsConcurrentFinalization(PostgresFailure("23503", constraint)).ShouldBeFalse();
	}

	[Fact]
	public void Optimistic_conflicts_are_retryable_but_unrelated_failures_are_not()
	{
		_classifier.IsConcurrentFinalization(new AbpDbConcurrencyException()).ShouldBeTrue();
		_classifier.IsConcurrentFinalization(new DbUpdateConcurrencyException()).ShouldBeTrue();
		_classifier.IsConcurrentFinalization(PostgresFailure("23505", "UX_Unrelated")).ShouldBeFalse();
		_classifier.IsConcurrentFinalization(new InvalidOperationException()).ShouldBeFalse();
	}

	[Fact]
	public void Sqlite_test_provider_matches_only_the_exact_identity_constraint()
	{
		_classifier.IsStudentIdentityConflict(SqliteFailure("AppStudents.Id")).ShouldBeTrue();
		_classifier.IsStudentIdentityConflict(SqliteFailure("AppStudents.OtherId")).ShouldBeFalse();
		_classifier.IsStudentIdentityConflict(new DbUpdateException("unique", new SqliteException(
			"SQLite Error 19: 'FOREIGN KEY constraint failed'.", 19))).ShouldBeFalse();
		_classifier.IsConcurrentFinalization(SqliteFailure(
			"AppStudentOnboardingCases.TenantId, AppStudentOnboardingCases.ApplicationId")).ShouldBeTrue();
	}

	private static DbUpdateException PostgresFailure(string sqlState, string constraint) => new("Save failed",
		new PostgresException("Synthetic database conflict", "ERROR", "ERROR", sqlState, constraintName: constraint));

	private static DbUpdateException SqliteFailure(string columns) => new("Save failed",
		new SqliteException($"SQLite Error 19: 'UNIQUE constraint failed: {columns}'.", 19));
}

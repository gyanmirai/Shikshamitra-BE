using System;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;

public sealed class AdmissionFinalizationConflictClassifier : IAdmissionFinalizationConflictClassifier, ITransientDependency
{
	public bool IsStudentIdentityConflict(Exception exception) => IsUniqueViolation(exception,
		"PK_AppStudents", "AppStudents.Id");

	public bool IsConcurrentFinalization(Exception exception)
	{
		for (var current = exception; current != null; current = current.InnerException)
		{
			if (current is AbpDbConcurrencyException or DbUpdateConcurrencyException) return true;
		}

		return IsUniqueViolation(exception, "IX_AppStudentOnboardingCases_TenantId_ApplicationId",
			"AppStudentOnboardingCases.TenantId, AppStudentOnboardingCases.ApplicationId") ||
			IsUniqueViolation(exception, "IX_AppStudentOnboardingCases_TenantId_IdempotencyKey",
				"AppStudentOnboardingCases.TenantId, AppStudentOnboardingCases.IdempotencyKey") ||
			IsUniqueViolation(exception, "IX_AppStudentOnboardingCases_TenantId_StudentId_AcademicYearId",
				"AppStudentOnboardingCases.TenantId, AppStudentOnboardingCases.StudentId, AppStudentOnboardingCases.AcademicYearId");
	}

	private static bool IsUniqueViolation(Exception exception, string constraintName, string sqliteColumns)
	{
		for (var current = exception; current != null; current = current.InnerException)
		{
			if (current is not DbUpdateException updateException) continue;
			if (updateException.InnerException is PostgresException postgres)
				return postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
					string.Equals(postgres.ConstraintName, constraintName, StringComparison.Ordinal);

			// SQLite is used by the integration test host, not the production repository.
			// Match its concrete provider and exact constraint text; never translate unrelated failures.
			if (updateException.InnerException is DbException sqlite &&
				sqlite.GetType().FullName == "Microsoft.Data.Sqlite.SqliteException")
				return sqlite.Message == $"SQLite Error 19: 'UNIQUE constraint failed: {sqliteColumns}'.";
		}
		return false;
	}
}

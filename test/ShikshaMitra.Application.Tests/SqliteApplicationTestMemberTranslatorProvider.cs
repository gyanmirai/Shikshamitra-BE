using System;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Sqlite.Query.Internal;

namespace ShikshaMitra;

// PostgreSQL supports DateTimeOffset.Date; SQLite's test provider does not.
// Keep the provider's standard translations and supply only this missing member.
#pragma warning disable EF1001 // Test-only extension of the pinned EF Core SQLite provider.
internal sealed class SqliteApplicationTestMemberTranslatorProvider : SqliteMemberTranslatorProvider
{
	public SqliteApplicationTestMemberTranslatorProvider(RelationalMemberTranslatorProviderDependencies dependencies)
		: base(dependencies) => AddTranslators([new CalendarDateTranslator(dependencies.SqlExpressionFactory)]);

	private sealed class CalendarDateTranslator(ISqlExpressionFactory sql) : IMemberTranslator
	{
		public SqlExpression? Translate(SqlExpression? instance, MemberInfo member, Type returnType,
			IDiagnosticsLogger<DbLoggerCategory.Query> logger)
		{
			if (instance == null || member.DeclaringType != typeof(DateTimeOffset) || member.Name != nameof(DateTimeOffset.Date))
				return null;

			// SQLite stores DateTimeOffset as ISO text. Taking its calendar prefix first
			// preserves .Date semantics instead of converting the offset to a UTC date.
			var date = sql.Function("substr", [instance, sql.Constant(1), sql.Constant(10)],
				nullable: true, argumentsPropagateNullability: [true, false, false], typeof(string));
			return sql.Function("datetime", [date], nullable: true,
				argumentsPropagateNullability: [true], returnType);
		}
	}
}
#pragma warning restore EF1001

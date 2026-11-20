using System.Globalization;
using Microsoft.Data.Sqlite;
using ShikshaMitra.Finance.ExternalReceivables;

namespace ShikshaMitra.Library;

// Test infrastructure only. Production remains PostgreSQL and uses M9/M10 pure
// functions. These are the exact Domain callbacks verified by provider tests.
internal static class FinanceExternalPositionSqliteTestFunctions
{
	public static void Register(SqliteConnection connection)
	{
		connection.CreateFunction<string?, string?>("AppFinanceTrySourceFingerprintV1",
			FinanceExternalSourceFingerprintV1.TryFromSnapshot, isDeterministic: true);
		connection.CreateFunction<string?, string?, string?, string?>("AppFinanceTryJsonScalar",
			FinanceExternalEvidenceJson.Scalar, isDeterministic: true);
		// TEXT is deliberate: SQLite REAL would lose valid decimal18,2 values.
		connection.CreateFunction<string?, string?, string?>("AppFinanceTryJsonMoney",
			(document, path) => FinanceExternalEvidenceJson.Money(document, path)?.ToString("0.00", CultureInfo.InvariantCulture),
			isDeterministic: true);
	}
}

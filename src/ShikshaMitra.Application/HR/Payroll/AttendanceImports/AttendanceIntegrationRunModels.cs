using System;
using System.Collections.Generic;
using ShikshaMitra.HR.AttendanceAndLeaves;

namespace ShikshaMitra.HR.Payroll.AttendanceImports;

public readonly record struct ParsedIntegrationRow(
	AttendanceIntegrationRunRowDto Row,
	DateOnly AttendanceDate);

public sealed class AttendanceIntegrationRunOutcome
{
	public int Created { get; set; }
	public int Replaced { get; set; }
	public List<AttendanceImportRow> ImportRows { get; } = [];
}

public sealed record AttendanceIntegrationImportRowOutcome(
	AttendanceImportRow Row,
	bool Created,
	bool Replaced);

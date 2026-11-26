using System.Diagnostics;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

public enum PayrollReadCheckpoint
{
	RoutingCompleted,
	AuthenticationCompleted,
	ActionStarted,
	ServiceStarted,
	AcademicYearResolved,
	StaffScopeResolved,
	PreviewResolved,
	CountRead,
	PageRead,
	DetailsRead,
	StatusRead,
	ActionCompleted
}

/// <summary>Emits fixed checkpoints without request, employee, or financial data.</summary>
public static class PayrollReadDiagnostics
{
	public const string SourceName = "ShikshaMitra.PayrollRead";
	public const string CheckpointEvent = "ShikshaMitra.PayrollRead.Checkpoint";
	private static readonly DiagnosticListener Listener = new(SourceName);

	public static void Mark(PayrollReadCheckpoint checkpoint)
	{
		if (Listener.IsEnabled(CheckpointEvent))
			Listener.Write(CheckpointEvent, checkpoint);
	}
}

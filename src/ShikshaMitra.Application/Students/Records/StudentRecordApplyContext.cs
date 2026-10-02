using System;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.Records;

/// <summary>Server-only, scoped authorization for an already validated Records command.</summary>
public sealed class StudentRecordApplyContext : IScopedDependency
{
	private Guid? _studentId;
	private string? _section;
	public bool Allows(Guid studentId, string section) => _studentId == studentId && _section == section;
	public IDisposable Begin(Guid studentId, string section)
	{
		var previousStudent = _studentId; var previousSection = _section;
		_studentId = studentId; _section = section;
		return new Reset(() => { _studentId = previousStudent; _section = previousSection; });
	}
	private sealed class Reset(Action reset) : IDisposable
	{
		public void Dispose() => reset();
	}
}

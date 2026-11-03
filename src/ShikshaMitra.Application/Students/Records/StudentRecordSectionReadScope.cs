using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.Records;

/// <summary>Canonical section rows for one Details read, never authority or a redacted view.</summary>
internal sealed class StudentRecordSectionReadScope(Guid studentId, Guid? tenantId, Guid? actorId, IUnitOfWork work)
{
	private readonly Dictionary<string, object?> rows = new(StringComparer.Ordinal);

	internal void RequireContext(Guid requestedStudentId, Guid? currentTenantId, Guid? currentActorId, IUnitOfWork? currentWork)
	{
		if (studentId != requestedStudentId || tenantId != currentTenantId || actorId != currentActorId || !ReferenceEquals(work, currentWork))
			throw new InvalidOperationException("Section material belongs to a different student, actor, tenant or operation.");
	}

	// Each owning reader executes its normal guard first, then maps a fresh DTO.
	// The cached repository row never escapes to a consumer that can redact it.
	internal async Task<T> ReadAsync<T>(string section, Func<Task<T>> read)
	{
		if (rows.TryGetValue(section, out var existing)) return (T)existing!;
		var value = await read(); rows.Add(section, value); return value;
	}
}

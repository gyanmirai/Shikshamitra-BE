using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.Records;

/// <summary>Immutable submitted form material for one student's single save transaction.</summary>
internal sealed class StudentRecordRegistrationReadScope(Guid studentId, Guid? tenantId, Guid? actorId, IUnitOfWork work)
{
	private readonly Dictionary<string, object> material = new(StringComparer.Ordinal);

	internal void RequireContext(Guid requestedStudentId, Guid? currentTenantId, Guid? currentActorId, IUnitOfWork? currentWork)
	{
		if (studentId != requestedStudentId || tenantId != currentTenantId || actorId != currentActorId || !ReferenceEquals(work, currentWork))
			throw new InvalidOperationException("Registration material belongs to a different student, actor, tenant or transaction.");
	}

	internal async Task<T> ReadImmutableAsync<T>(string key, Func<Task<T>> read)
	{
		if (material.TryGetValue(key, out var existing)) return (T)existing;
		var value = await read();
		material.Add(key, value!);
		return value;
	}

	internal static string Key(string resource, IEnumerable<Guid> ids) =>
		resource + ":" + string.Join(",", ids.Distinct().OrderBy(id => id).Select(id => id.ToString("N")));
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;

public class EfCoreAdmissionAssessmentResourceLock(IDbContextProvider<ShikshaMitraDbContext> provider)
	: IAdmissionAssessmentResourceLock, ITransientDependency
{
	public async Task AcquireAsync(Guid? tenantId, IEnumerable<Guid> resourceIds)
	{
		var db = await provider.GetDbContextAsync();
		if (db.Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL") return;
		if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Assessment scheduling requires a transactional unit of work.");
		// A common tenant scheduling lock coordinates legacy room writers and compound assessment commands.
		// It remains held until commit, including nested room-service calls.
		foreach (var id in new[] { Guid.Empty }.Concat(resourceIds.Where(id => id != Guid.Empty).Distinct().Order()))
		{
			var digest = SHA256.HashData(Encoding.UTF8.GetBytes($"assessment-resource:{tenantId}:{id}"));
			var key = BitConverter.ToInt64(digest, 0);
			await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})");
		}
	}
}

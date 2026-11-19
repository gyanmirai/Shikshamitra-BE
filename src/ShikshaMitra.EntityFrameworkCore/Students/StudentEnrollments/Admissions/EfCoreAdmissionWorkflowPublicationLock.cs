using System;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;

public class EfCoreAdmissionWorkflowPublicationLock(IDbContextProvider<ShikshaMitraDbContext> provider)
	: IAdmissionWorkflowPublicationLock, ITransientDependency
{
	public async Task AcquireAsync(Guid? tenantId, string workflowCode)
	{
		var db = await provider.GetDbContextAsync();
		if (db.Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL") return;
		if (db.Database.CurrentTransaction == null)
			throw new InvalidOperationException("Workflow publication requires a transactional unit of work.");
		var digest = SHA256.HashData(Encoding.UTF8.GetBytes($"workflow-publication:{tenantId:N}:{workflowCode}"));
		var key = BinaryPrimitives.ReadInt64BigEndian(digest);
		// Publish and Re-open hold the same family lock through commit, including across API hosts.
		await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})");
	}
}

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;

public class EfCoreAdmissionIntakeProfileRepository(
	IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
	: EfCoreRepository<ShikshaMitraDbContext, AdmissionIntakeProfile, Guid>(dbContextProvider),
		IAdmissionIntakeProfileRepository
{
	public async Task AcquireAssignmentLockAsync(Guid profileId, CancellationToken cancellationToken = default)
	{
		var dbContext = await GetDbContextAsync();
		if (dbContext.Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL") return;
		if (dbContext.Database.CurrentTransaction == null)
			throw new InvalidOperationException("Automatic admission assignment requires a transactional unit of work.");
		var bytes = profileId.ToByteArray();
		var lockKey = BitConverter.ToInt64(bytes, 0) ^ BitConverter.ToInt64(bytes, 8);
		await dbContext.Database.ExecuteSqlInterpolatedAsync(
			$"SELECT pg_advisory_xact_lock({lockKey})", cancellationToken);
	}
}

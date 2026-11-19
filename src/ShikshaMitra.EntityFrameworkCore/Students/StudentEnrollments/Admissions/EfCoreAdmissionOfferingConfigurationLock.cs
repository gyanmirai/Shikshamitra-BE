using System;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;

public class EfCoreAdmissionOfferingConfigurationLock(
	IDbContextProvider<ShikshaMitraDbContext> provider,
	ICurrentTenant currentTenant) : IAdmissionOfferingConfigurationLock, ITransientDependency
{
	public async Task AcquireAndRefreshAsync(AdmissionOffering offering)
	{
		var db = await provider.GetDbContextAsync();
		if (db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
		{
			if (db.Database.CurrentTransaction == null)
				throw new InvalidOperationException("Admission intake requires a transactional unit of work.");
			var digest = SHA256.HashData(Encoding.UTF8.GetBytes($"offering-configuration:{currentTenant.Id:N}:{offering.Id:N}"));
			var key = BinaryPrimitives.ReadInt64BigEndian(digest);
			await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})");
		}
		var entry = db.Entry(offering);
		// Never discard pending work or attach a second copy. Current capture paths provide an unchanged tracked aggregate.
		if (entry.State != EntityState.Unchanged)
			throw new BusinessException(AdmissionErrorCodes.OfferingConcurrencyConflict,
				"Refresh the admission offering before continuing intake.");
		await entry.ReloadAsync();
		if (entry.State == EntityState.Detached || offering.IsDeleted || offering.TenantId != currentTenant.Id)
			throw new EntityNotFoundException(typeof(AdmissionOffering), offering.Id);
	}
}

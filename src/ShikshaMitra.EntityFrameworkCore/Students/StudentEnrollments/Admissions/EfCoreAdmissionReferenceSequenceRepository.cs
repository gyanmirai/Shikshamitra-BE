using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;

public class EfCoreAdmissionReferenceSequenceRepository(
	IDbContextProvider<ShikshaMitraDbContext> dbContextProvider,
	ICurrentTenant currentTenant)
	: EfCoreRepository<ShikshaMitraDbContext, AdmissionReferenceSequence, Guid>(dbContextProvider),
		IAdmissionReferenceSequenceRepository
{
	public async Task<long> IncrementAndGetAsync(
		int year,
		AdmissionReferenceKind kind,
		CancellationToken cancellationToken = default)
	{
		var storedTenantId = currentTenant.Id ?? AdmissionReferenceSequence.HostTenantSentinel;
		for (var retry = 0; retry < 10; retry++)
		{
			try
			{
				var dbContext = await GetDbContextAsync();
				var sequence = await dbContext.Set<AdmissionReferenceSequence>()
					.SingleOrDefaultAsync(item =>
						item.StoredTenantId == storedTenantId && item.Year == year && item.Kind == kind,
						cancellationToken);
				if (sequence == null)
				{
					sequence = AdmissionReferenceSequence.Create(Guid.NewGuid(), currentTenant.Id, year, kind);
					await dbContext.Set<AdmissionReferenceSequence>().AddAsync(sequence, cancellationToken);
				}

				var value = sequence.TakeNext();
				await dbContext.SaveChangesAsync(cancellationToken);
				return value;
			}
			catch (Exception exception) when (exception is DbUpdateConcurrencyException or DbUpdateException)
			{
				var dbContext = await GetDbContextAsync();
				dbContext.ChangeTracker.Clear();
				if (retry == 9) throw;
				await Task.Delay(5, cancellationToken);
			}
		}

		throw new InvalidOperationException("Unable to allocate an admission reference number.");
	}
}

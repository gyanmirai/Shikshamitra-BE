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

public class EfCoreAdmissionPublicInquirySubmissionLock(IDbContextProvider<ShikshaMitraDbContext> provider)
	: IAdmissionPublicInquirySubmissionLock, ITransientDependency
{
	public async Task AcquireAsync(Guid? tenantId, Guid linkId)
	{
		var db = await provider.GetDbContextAsync();
		if (db.Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL") return;
		if (db.Database.CurrentTransaction == null)
			throw new InvalidOperationException("Public inquiry capture requires a transactional unit of work.");
		var digest = SHA256.HashData(Encoding.UTF8.GetBytes($"public-inquiry-submission:{tenantId:N}:{linkId:N}"));
		var key = BinaryPrimitives.ReadInt64BigEndian(digest);
		// Covers keyed and legacy replay checks together and remains held through commit across hosts.
		await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})");
	}
}

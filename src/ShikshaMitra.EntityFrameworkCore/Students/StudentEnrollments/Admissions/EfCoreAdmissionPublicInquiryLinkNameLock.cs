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

public class EfCoreAdmissionPublicInquiryLinkNameLock(IDbContextProvider<ShikshaMitraDbContext> provider)
	: IAdmissionPublicInquiryLinkNameLock, ITransientDependency
{
	public async Task AcquireAsync(Guid? tenantId, string normalizedName)
	{
		var db = await provider.GetDbContextAsync();
		if (db.Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL") return;
		if (db.Database.CurrentTransaction == null)
			throw new InvalidOperationException("Inquiry link name validation requires a transactional unit of work.");
		var digest = SHA256.HashData(Encoding.UTF8.GetBytes($"public-inquiry-link-name:{tenantId:N}:{normalizedName}"));
		var key = BinaryPrimitives.ReadInt64BigEndian(digest);
		await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})");
	}
}

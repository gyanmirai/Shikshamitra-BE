using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;

public class EfCoreAdmissionPublicInquirySubmissionRepository(IDbContextProvider<ShikshaMitraDbContext> provider)
	: IAdmissionPublicInquirySubmissionRepository, ITransientDependency
{
	public async Task<string?> FindFingerprintAsync(Guid? tenantId, Guid cycleId, Guid linkId, Guid? requestId, string fingerprint)
	{
		var db = await provider.GetDbContextAsync();
		var link = linkId.ToString("N");
		var property = requestId.HasValue ? AdmissionPublicInquirySubmissionProperties.RequestId : AdmissionPublicInquirySubmissionProperties.Fingerprint;
		var value = requestId.HasValue ? requestId.Value.ToString("N") : fingerprint;
		var linkProperty = AdmissionPublicInquirySubmissionProperties.LinkId;
		var fingerprintProperty = AdmissionPublicInquirySubmissionProperties.Fingerprint;
		// Return one scalar only. JSON predicates stay in the database as the inquiry population grows.
		return db.Database.ProviderName switch
		{
			"Npgsql.EntityFrameworkCore.PostgreSQL" => await db.Database.SqlQuery<string>($"""
				SELECT CAST("ExtraProperties" AS jsonb)->>{fingerprintProperty} AS "Value" FROM "AppAdmissionInquiries"
				WHERE "TenantId" IS NOT DISTINCT FROM {tenantId} AND NOT "IsDeleted" AND "AdmissionCycleId" = {cycleId}
				AND CAST("ExtraProperties" AS jsonb)->>{linkProperty} = {link}
				AND CAST("ExtraProperties" AS jsonb)->>{property} = {value}
				ORDER BY "Id" LIMIT 1
				""").SingleOrDefaultAsync(),
			"Microsoft.EntityFrameworkCore.Sqlite" => await db.Database.SqlQuery<string>($"""
				SELECT json_extract("ExtraProperties", {"$." + fingerprintProperty}) AS "Value" FROM "AppAdmissionInquiries"
				WHERE "TenantId" IS {tenantId} AND NOT "IsDeleted" AND "AdmissionCycleId" = {cycleId}
				AND json_extract("ExtraProperties", {"$." + linkProperty}) = {link}
				AND json_extract("ExtraProperties", {"$." + property}) = {value}
				ORDER BY "Id" LIMIT 1
				""").SingleOrDefaultAsync(),
			_ => throw new NotSupportedException("Public inquiry replay lookup requires the configured PostgreSQL or SQLite provider.")
		};
	}
}

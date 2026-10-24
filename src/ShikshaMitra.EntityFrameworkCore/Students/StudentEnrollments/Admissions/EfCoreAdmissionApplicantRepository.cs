using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;

public class EfCoreAdmissionApplicantRepository(IDbContextProvider<ShikshaMitraDbContext> dbContextProvider, ICurrentTenant currentTenant)
	: EfCoreRepository<ShikshaMitraDbContext, AdmissionApplicant, Guid>(dbContextProvider), IAdmissionApplicantRepository
{
	public async Task<IQueryable<AdmissionApplicant>> GetParentContactMatchesAsync(IReadOnlyCollection<string> emails, IReadOnlyCollection<string> mobiles)
		=> BuildParentContactQuery(await GetDbContextAsync(), currentTenant.Id, emails, mobiles);

	internal static IQueryable<AdmissionApplicant> BuildParentContactQuery(ShikshaMitraDbContext context, Guid? tenantId,
		IReadOnlyCollection<string> emails, IReadOnlyCollection<string> mobiles)
	{
		ArgumentNullException.ThrowIfNull(emails);
		ArgumentNullException.ThrowIfNull(mobiles);
		if (emails.Count == 0 && mobiles.Count == 0) return context.Set<AdmissionApplicant>().Where(_ => false);
		var emailJson = JsonSerializer.Serialize(emails);
		var mobileJson = JsonSerializer.Serialize(mobiles);
		return context.Database.ProviderName switch
		{
			"Npgsql.EntityFrameworkCore.PostgreSQL" => context.Set<AdmissionApplicant>().FromSqlInterpolated($"""
				SELECT * FROM "AppAdmissionApplicants"
				WHERE "TenantId" IS NOT DISTINCT FROM {tenantId} AND NOT "IsDeleted" AND "ExistingStudentId" IS NULL
				AND (
				 EXISTS (SELECT 1 FROM jsonb_array_elements_text(CAST({emailJson} AS jsonb)) AS contact(value)
				   WHERE lower(btrim(CAST("ExtraProperties" AS jsonb)->>'AdmissionStudentField.Father.Email')) = contact.value
				      OR lower(btrim(CAST("ExtraProperties" AS jsonb)->>'AdmissionStudentField.Mother.Email')) = contact.value)
				 OR EXISTS (SELECT 1 FROM jsonb_array_elements_text(CAST({mobileJson} AS jsonb)) AS contact(value)
				   WHERE regexp_replace(CAST("ExtraProperties" AS jsonb)->>'AdmissionStudentField.Father.Mobile', '[^0-9]', '', 'g') IN (contact.value, '91' || contact.value)
				      OR regexp_replace(CAST("ExtraProperties" AS jsonb)->>'AdmissionStudentField.Mother.Mobile', '[^0-9]', '', 'g') IN (contact.value, '91' || contact.value))
				)
				"""),
			"Microsoft.EntityFrameworkCore.Sqlite" => context.Set<AdmissionApplicant>().FromSqlInterpolated($"""
				SELECT * FROM "AppAdmissionApplicants"
				WHERE "TenantId" IS {tenantId} AND NOT "IsDeleted" AND "ExistingStudentId" IS NULL
				AND (
				 EXISTS (SELECT 1 FROM json_each({emailJson}) AS contact
				   WHERE lower(trim(json_extract("ExtraProperties", '$."AdmissionStudentField.Father.Email"'))) = contact.value
				      OR lower(trim(json_extract("ExtraProperties", '$."AdmissionStudentField.Mother.Email"'))) = contact.value)
				 OR EXISTS (SELECT 1 FROM json_each({mobileJson}) AS contact
				   WHERE replace(replace(replace(replace(replace(json_extract("ExtraProperties", '$."AdmissionStudentField.Father.Mobile"'), ' ', ''), '+', ''), '-', ''), '(', ''), ')', '') IN (contact.value, '91' || contact.value)
				      OR replace(replace(replace(replace(replace(json_extract("ExtraProperties", '$."AdmissionStudentField.Mother.Mobile"'), ' ', ''), '+', ''), '-', ''), '(', ''), ')', '') IN (contact.value, '91' || contact.value))
				)
				"""),
			_ => throw new NotSupportedException("Saved parent contact matching requires the configured PostgreSQL or SQLite provider.")
		};
	}
}

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class EfCoreAdmissionInquiryLeadRepository(
	IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
	: EfCoreRepository<ShikshaMitraDbContext, AdmissionInquiryLead, Guid>(dbContextProvider)
{
	private static int _sqliteNextApplicantNumber;

	public override async Task<AdmissionInquiryLead> InsertAsync(
		AdmissionInquiryLead entity,
		bool autoSave = false,
		CancellationToken cancellationToken = default)
	{
		await EnsureSqliteApplicantNumberAsync(entity, cancellationToken);
		return await base.InsertAsync(entity, autoSave, cancellationToken);
	}

	private async Task EnsureSqliteApplicantNumberAsync(
		AdmissionInquiryLead entity,
		CancellationToken cancellationToken)
	{
		if (entity.ApplicantNumber > 0)
		{
			return;
		}

		var dbContext = await GetDbContextAsync();
		if (!string.Equals(
			    dbContext.Database.ProviderName,
			    "Microsoft.EntityFrameworkCore.Sqlite",
			    StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		var maxApplicantNumber = await dbContext.Set<AdmissionInquiryLead>()
			.Select(x => (int?)x.ApplicantNumber)
			.MaxAsync(cancellationToken) ?? 0;
		var nextApplicantNumber = Math.Max(
			Interlocked.Increment(ref _sqliteNextApplicantNumber),
			maxApplicantNumber + 1);

		typeof(AdmissionInquiryLead)
			.GetProperty(nameof(AdmissionInquiryLead.ApplicantNumber))!
			.SetValue(entity, nextApplicantNumber);
	}
}

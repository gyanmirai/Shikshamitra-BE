using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Charges;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Fees.Charges;

/// <summary>
/// EF Core implementation of <see cref="IChargeRepository"/>.
/// Owns the multi-table join query so the application layer stays thin.
/// </summary>
public class EfCoreChargeRepository(
	IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
	: EfCoreRepository<ShikshaMitraDbContext, Charge, Guid>(dbContextProvider), IChargeRepository
{
	/// <inheritdoc />
	public async Task<ChargeJoinResult?> GetChargeWithDetailsAsync(
		Guid chargeId,
		CancellationToken cancellationToken = default)
	{
		var db = await GetDbContextAsync();

		var result = await (
			from charge in db.Set<Charge>().IgnoreAutoIncludes().AsNoTracking()
			join student in db.Set<Student>().IgnoreAutoIncludes().AsNoTracking() on charge.StudentId equals student.Id
			join category in db.Set<ChargeCategory>().IgnoreAutoIncludes().AsNoTracking() on charge.ChargeCategoryId equals category.Id
			join standard in db.Set<Standard>().IgnoreAutoIncludes().AsNoTracking() on charge.StandardId equals standard.Id
			join division in db.Set<Division>().IgnoreAutoIncludes().AsNoTracking() on charge.DivisionId equals division.Id
			join board in db.Set<EducationBoard>().IgnoreAutoIncludes().AsNoTracking() on charge.BoardId equals board.Id
			join medium in db.Set<EducationMedium>().IgnoreAutoIncludes().AsNoTracking() on charge.EducationMediumId equals medium.Id
			join academicYear in db.Set<AcademicYear>().IgnoreAutoIncludes().AsNoTracking() on charge.AcademicYearId equals academicYear.Id
			join requestor in db.Set<IdentityUser>().IgnoreAutoIncludes().AsNoTracking() on charge.RequestorId equals requestor.Id
			where charge.Id == chargeId
			select new ChargeJoinResult(charge, student, category, standard, division, board, medium, academicYear, requestor)
		).FirstOrDefaultAsync(cancellationToken);

		return result;
	}

	/// <inheritdoc />
	public async Task<IQueryable<ChargeJoinResult>> GetChargesQueryAsync(
		Guid academicYearId,
		int status,
		Guid? boardId = null,
		Guid? educationMediumId = null,
		string? search = null)
	{
		var db = await GetDbContextAsync();
		var chargeStatus = (ChargeStatus)status;
		var searchLower = search?.ToLower();

		return from charge in db.Set<Charge>().IgnoreAutoIncludes().AsNoTracking()
			join student in db.Set<Student>().IgnoreAutoIncludes().AsNoTracking() on charge.StudentId equals student.Id
			join category in db.Set<ChargeCategory>().IgnoreAutoIncludes().AsNoTracking() on charge.ChargeCategoryId equals category.Id
			join standard in db.Set<Standard>().IgnoreAutoIncludes().AsNoTracking() on charge.StandardId equals standard.Id
			join division in db.Set<Division>().IgnoreAutoIncludes().AsNoTracking() on charge.DivisionId equals division.Id
			join board in db.Set<EducationBoard>().IgnoreAutoIncludes().AsNoTracking() on charge.BoardId equals board.Id
			join medium in db.Set<EducationMedium>().IgnoreAutoIncludes().AsNoTracking() on charge.EducationMediumId equals medium.Id
			join academicYear in db.Set<AcademicYear>().IgnoreAutoIncludes().AsNoTracking() on charge.AcademicYearId equals academicYear.Id
			join requestor in db.Set<IdentityUser>().IgnoreAutoIncludes().AsNoTracking() on charge.RequestorId equals requestor.Id
			where charge.AcademicYearId == academicYearId
			where charge.Status == chargeStatus
			where !boardId.HasValue || charge.BoardId == boardId.Value
			where !educationMediumId.HasValue || charge.EducationMediumId == educationMediumId.Value
			where searchLower == null
				|| (student.FirstName + " " + student.MiddleName + " " + student.LastName).ToLower().Contains(searchLower)
				|| student.GrNo.ToString()!.Contains(searchLower)
				|| student.RollNo.ToString()!.Contains(searchLower)
			select new ChargeJoinResult(charge, student, category, standard, division, board, medium, academicYear, requestor);
	}
}

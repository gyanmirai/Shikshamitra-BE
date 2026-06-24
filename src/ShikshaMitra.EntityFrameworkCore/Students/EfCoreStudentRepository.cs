using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace ShikshaMitra.Students;

/// <summary>
/// EF Core implementation of <see cref="IStudentRepository"/>.
/// </summary>
public class EfCoreStudentRepository(
    IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
    : EfCoreRepository<ShikshaMitraDbContext, Student, Guid>(dbContextProvider), IStudentRepository
{
    /// <inheritdoc/>
    public async Task<IQueryable<Student>> GetQueryableWithoutAutoIncludesAsync()
    {
        var dbSet = await GetDbSetAsync();
        return dbSet.IgnoreAutoIncludes().AsNoTracking();
    }

    /// <inheritdoc/>
    public async Task<Student?> GetInquiryForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet
            .IgnoreAutoIncludes()
            .Include(s => s.AdditionalDetail)
            .Include(s => s.ContactDetail)
            .Include(s => s.FamilyDetail)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Student?> GetRegistrationAggregateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet
            .IgnoreAutoIncludes()
            .Include(s => s.AdditionalDetail)
            .Include(s => s.ContactDetail)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Student?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet
            .Include(s => s.AdditionalDetail)
            .Include(s => s.ContactDetail)
            .Include(s => s.FamilyDetail)
            .Include(s => s.CommunicationDetail)
            .Include(s => s.MedicalRecord)
            .Include(s => s.PreviousSchoolDetail)
            .Include(s => s.BankDetail)
            .Include(s => s.Notes)
            .Include(s => s.StatusHistories)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Student?> GetByGrNoAsync(int grNo, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet
            .FirstOrDefaultAsync(s => s.GrNo == grNo && s.TenantId == CurrentTenant.Id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<Student>> GetByDivisionAsync(
        Guid divisionId,
        Guid academicYearId,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet
            .Where(s => s.DivisionId == divisionId && s.AcademicYearId == academicYearId)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<Student>> GetActiveStudentsInScopeAsync(
        Guid academicYearId,
        Guid boardId,
        Guid educationMediumId,
        IReadOnlyList<Guid> classIds,
        IReadOnlyList<Guid>? divisionIds = null,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        var classIdSet = classIds.ToList();

        var query = dbSet.IgnoreAutoIncludes().Where(s =>
            s.AcademicYearId == academicYearId
            && s.AcademicBoardId == boardId
            && s.AcademicMediumId == educationMediumId
            && s.StandardId != null
            && classIdSet.Contains(s.StandardId.Value)
            && s.Status == StudentLifeCycleStatusEnum.Enrolled);

        if (divisionIds is { Count: > 0 })
        {
            var divisionIdSet = divisionIds.ToList();
            query = query.Where(s => s.DivisionId != null && divisionIdSet.Contains(s.DivisionId.Value));
        }

        return await query.ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Dictionary<int, Student>> GetStudentsByGrNosAsync(
        IEnumerable<int> grNos,
        CancellationToken cancellationToken = default)
    {
        var grNoList = grNos.ToList();
        var dbSet = await GetDbSetAsync();
        var students = await dbSet
            .Where(s => s.GrNo != null && grNoList.Contains(s.GrNo!.Value) && s.TenantId == CurrentTenant.Id)
            .ToListAsync(cancellationToken);

        return students
            .Where(s => s.GrNo.HasValue)
            .ToDictionary(s => s.GrNo!.Value);
    }

    /// <inheritdoc/>
    public async Task<int> GetEnrolledCountForYearAsync(
        Guid? tenantId,
        int academicYear,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet
            .Where(s => s.TenantId == tenantId && s.AcademicYearId != null)
            .CountAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> GetMaxInquirySequenceForMediumAsync(
        Guid academicMediumId,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();

        // Fetch only the InquiryNo strings that match the prefix — EF translates StartsWith to LIKE 'INQ%'
        var inquiryNumbers = await dbSet
            .Where(s => s.AcademicMediumId == academicMediumId
                     && s.TenantId == tenantId
                     && s.AdditionalDetail != null
                     && s.AdditionalDetail.InquiryNo != null
                     && s.AdditionalDetail.InquiryNo.StartsWith("INQ"))
            .Select(s => s.AdditionalDetail!.InquiryNo)
            .ToListAsync(cancellationToken);

        // Parse the numeric suffix on the application side to avoid DB-level cast errors
        int maxSequence = 0;
        foreach (var inquiryNo in inquiryNumbers)
        {
            if (inquiryNo is { Length: >= 4 }
                && int.TryParse(inquiryNo.AsSpan(3), out var n)
                && n > maxSequence)
            {
                maxSequence = n;
            }
        }

        return maxSequence;
    }
}

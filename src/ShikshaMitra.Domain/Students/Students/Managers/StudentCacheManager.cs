using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students;

/// <summary>
/// Cache item for student data.
/// Stored in distributed cache to avoid redundant DB round-trips.
/// Mirrors <c>EmployeeCacheItem</c> in the HR module.
/// </summary>
[Serializable]
public class StudentCacheItem
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    /// <summary>Computed full name: FirstName + " " + LastName.</summary>
    public string? FullName => string.Join(" ",
        new[] { FirstName, LastName }.WhereNotNullOrWhiteSpace());

    public int? GrNo { get; set; }
    public int? RollNo { get; set; }
    public Guid? StandardId { get; set; }
    public Guid? DivisionId { get; set; }
    public Guid? AcademicYearId { get; set; }
    public StudentLifeCycleStatusEnum Status { get; set; }

    /// <summary>UTC timestamp of when this item was written to cache.</summary>
    public DateTime CachedAt { get; set; }
}

/// <summary>
/// Provides consistent, tenant-aware cache key generation for student lookups.
/// Mirrors <c>EmployeeCacheKeyProvider</c> in the HR module.
/// </summary>
public static class StudentCacheKeyProvider
{
    /// <summary>Cache key by Student ID. Pattern: "st:id:{studentId}"</summary>
    public static string ById(Guid studentId)           => $"st:id:{studentId}";

    /// <summary>Cache key by GR number. Pattern: "st:gr:{grNo}:{tenantId}"</summary>
    public static string ByGrNo(int grNo, Guid? tenantId) => $"st:gr:{grNo}:{tenantId ?? Guid.Empty}";

    /// <summary>Cache key by division+year. Pattern: "st:div:{divisionId}:{academicYearId}"</summary>
    public static string ByDivision(Guid divisionId, Guid academicYearId) => $"st:div:{divisionId}:{academicYearId}";
}

/// <summary>
/// Domain-layer manager for student distributed cache operations.
/// Handles cache invalidation, retrieval, and storage.
/// Mirrors <c>IEmployeeCacheManager</c> in the HR module.
/// </summary>
public interface IStudentCacheManager
{
    /// <summary>Gets the student cache item by ID, loading from DB if not cached.</summary>
    Task<StudentCacheItem?> GetOrAddByIdAsync(Guid studentId, CancellationToken cancellationToken = default);

    /// <summary>Gets the student cache item by GR number, loading from DB if not cached.</summary>
    Task<StudentCacheItem?> GetOrAddByGrNoAsync(int grNo, CancellationToken cancellationToken = default);

    /// <summary>Removes all cache entries for a given student (by ID and GR number).</summary>
    Task InvalidateAsync(Guid studentId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation of <see cref="IStudentCacheManager"/>.
/// Uses ABP's <see cref="IDistributedCache{TCacheItem}"/> for storage.
/// </summary>
public class StudentCacheManager(
    IDistributedCache<StudentCacheItem> cache,
    IStudentRepository studentRepository
) : IStudentCacheManager, ITransientDependency
{
    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
    };

    /// <inheritdoc/>
    public async Task<StudentCacheItem?> GetOrAddByIdAsync(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        var key = StudentCacheKeyProvider.ById(studentId);
        return await cache.GetOrAddAsync(
            key,
            async () => (await BuildCacheItemAsync(studentId, cancellationToken))!,
            () => CacheOptions,
            token: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<StudentCacheItem?> GetOrAddByGrNoAsync(
        int grNo,
        CancellationToken cancellationToken = default)
    {
        var student = await studentRepository.GetByGrNoAsync(grNo, cancellationToken);
        if (student == null) return null;
        return await GetOrAddByIdAsync(student.Id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task InvalidateAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        var item = await cache.GetAsync(StudentCacheKeyProvider.ById(studentId), token: cancellationToken);
        await cache.RemoveAsync(StudentCacheKeyProvider.ById(studentId), token: cancellationToken);

        if (item?.GrNo != null)
        {
            await cache.RemoveAsync(
                StudentCacheKeyProvider.ByGrNo(item.GrNo.Value, item.TenantId),
                token: cancellationToken);
        }
    }

    private async Task<StudentCacheItem?> BuildCacheItemAsync(
        Guid studentId,
        CancellationToken cancellationToken)
    {
        var student = await studentRepository.FindAsync(studentId, cancellationToken: cancellationToken);
        if (student == null) return null;

        return new StudentCacheItem
        {
            Id            = student.Id,
            TenantId      = student.TenantId,
            FirstName     = student.FirstName,
            LastName      = student.LastName,
            GrNo          = student.GrNo,
            RollNo        = student.RollNo,
            StandardId    = student.StandardId,
            DivisionId    = student.DivisionId,
            AcademicYearId = student.AcademicYearId,
            Status        = student.Status,
            CachedAt      = DateTime.UtcNow,
        };
    }
}

file static class StringExtensions
{
    public static IEnumerable<string> WhereNotNullOrWhiteSpace(this string?[] source)
    {
        foreach (var s in source)
            if (!string.IsNullOrWhiteSpace(s))
                yield return s!;
    }
}

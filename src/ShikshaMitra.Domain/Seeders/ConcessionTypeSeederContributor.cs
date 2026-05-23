using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Common;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

/// <summary>
/// Seeds three starter <see cref="ConcessionType"/> rows so developers can immediately
/// exercise the full concession wizard without configuring types from scratch.
///
/// Seeding strategy
/// ────────────────
/// • Idempotent: exits early when any <c>ConcessionType</c> already exists for the
///   active academic year of the tenant.
/// • Depends on <see cref="AcademicSetupSeederContributor"/> (an active academic year
///   must already exist).
/// • Seeds:
///   1. Sibling Discount       – Flat amount, doc: "Sibling Enrollment Certificate"
///   2. Merit Scholarship      – Percentage, doc: "Mark Sheet / Report Card"
///   3. Staff Ward Concession  – Flat amount, doc: "Employment Certificate"
/// </summary>
public class ConcessionTypeSeederContributor : BaseSeederContributor
{
    private readonly IAcademicYearRepository _academicYearRepository;
    private readonly IRepository<ConcessionType, Guid> _concessionTypeRepository;
    private readonly ILogger<ConcessionTypeSeederContributor> _logger;

    public ConcessionTypeSeederContributor(
        ICurrentTenant currentTenant,
        IGuidGenerator guidGenerator,
        IUnitOfWork unitOfWork,
        IAcademicYearRepository academicYearRepository,
        IRepository<ConcessionType, Guid> concessionTypeRepository,
        ILogger<ConcessionTypeSeederContributor> logger)
        : base(currentTenant, guidGenerator, unitOfWork)
    {
        _academicYearRepository   = academicYearRepository;
        _concessionTypeRepository = concessionTypeRepository;
        _logger                   = logger;
    }

    [UnitOfWork]
    public override async Task SeedAsync(DataSeedContext context)
    {
        // Use the same pattern as other seeders: try current year first, then any year
        var year = DateTime.UtcNow.Year;
        var activeYear = await _academicYearRepository.FirstOrDefaultAsync(y => y.Name.StartsWith(year.ToString()))
                      ?? await _academicYearRepository.FirstOrDefaultAsync();

        if (activeYear is null)
        {
            _logger.LogWarning("[ConcessionTypeSeeder] No academic year found — skipping.");
            return;
        }

        // Idempotency check: skip if any concession type already exists for this year
        var alreadySeeded = await _concessionTypeRepository.AnyAsync(
            ct => ct.AcademicYearId == activeYear.Id);

        if (alreadySeeded)
        {
            _logger.LogInformation("[ConcessionTypeSeeder] Concession types already seeded — skipping.");
            return;
        }

        var definitions = new[]
        {
            new
            {
                Name         = "Sibling Discount",
                Description  = "Applicable when a sibling is enrolled in the same school.",
                DiscountType = AmountType.Flat,
                DocName      = "Sibling Enrollment Certificate",
            },
            new
            {
                Name         = "Merit Scholarship",
                Description  = "Awarded to students who scored above 90% in the previous academic year.",
                DiscountType = AmountType.Percentage,
                DocName      = "Mark Sheet / Report Card",
            },
            new
            {
                Name         = "Staff Ward Concession",
                Description  = "Applicable to children of current school staff members.",
                DiscountType = AmountType.Flat,
                DocName      = "Employment Certificate",
            },
        };

        foreach (var def in definitions)
        {
            var concessionType = new ConcessionType(
                GuidGenerator.Create(),
                CurrentTenant.Id,
                activeYear.Id,
                def.Name,
                def.Description,
                isActive: true,
                discountType: def.DiscountType);

            concessionType.AddDocument(
                def.DocName,
                documentSize: 5_242_880L,
                acceptedFormats: new List<DocumentType> { DocumentType.Pdf, DocumentType.Jpg, DocumentType.Jpeg, DocumentType.Png });

            await _concessionTypeRepository.InsertAsync(concessionType);
            _logger.LogInformation("[ConcessionTypeSeeder] Seeded concession type: {Name}", def.Name);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Charges;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.FeeCollection;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Fees;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

[Authorize]
[RemoteService(false)]
public class FeeCollectionAppService(
    IRepository<FeeRecordMetadata, Guid> feeRecordMetadataRepository,
    IRepository<FeeCollectionRecord, Guid> feeCollectionRecordRepository,
    IRepository<ChargeCollectionRecord, Guid> chargeCollectionRecordRepository,
    IRepository<PaymentMethodRecord, Guid> paymentMethodRecordRepository,
    IRepository<FeeHead, Guid> feeHeadRepository,
    IRepository<Charge, Guid> chargeRepository,
    IRepository<AcademicYear, Guid> academicYearRepository,
    IRepository<StudentFeeRecord, Guid> studentFeeRecordRepository,
    IRepository<Student, Guid> studentRepository,
    IRepository<StudentFeeStructure, Guid> studentFeeStructureRepository,
    IRepository<FeeStructure, Guid> feeStructureRepository,
    IRepository<FeeStructureDetail, Guid> feeStructureDetailRepository,
    IRepository<FeeCategory, Guid> feeCategoryRepository,
    IConcessionRequestRepository concessionRequestRepository,
    IRepository<ConcessionType, Guid> concessionTypeRepository,
    IDueFeeRepository dueFeeRepository,
    ICurrentUser currentUser
) : ApplicationService, IFeeCollectionAppService
{
    [Authorize(ShikshaMitraSubModulePermissions.FeeCollection)]
    public async Task<StudentFeeCollectionListDto> GetStudentsAsync(GetStudentsInput input)
    {
        // ── 1. Build base student filter ──────────────────────────────────
        var studentsQuery = await studentRepository.GetQueryableAsync();

        var baseQuery = studentsQuery
            .Where(s => s.AcademicYearId == input.AcademicYearId
                     && s.AcademicBoardId == input.BoardId
                     && s.AcademicMediumId == input.EducationMediumId
                     && s.Status == StudentLifeCycleStatusEnum.Enrolled);

        if (!string.IsNullOrWhiteSpace(input.Search) && input.Search.Length >= 2)
        {
            var term = input.Search.Trim().ToLower();
            baseQuery = baseQuery.Where(s =>
                (s.FirstName != null && s.FirstName.ToLower().Contains(term)) ||
                (s.MiddleName != null && s.MiddleName.ToLower().Contains(term)) ||
                (s.LastName != null && s.LastName.ToLower().Contains(term)) ||
                (s.RollNo != null && s.RollNo.ToString()!.Contains(term)));
        }

        // Load all matching students so we can compute status for each one.
        // For a school dataset this is bounded by enrolment size; the status
        // filter cannot be pushed to the DB without pre-computed columns.
        var allStudents = baseQuery
            .OrderBy(s => s.RollNo)
            .ThenBy(s => s.FirstName)
            .ToList();

        if (allStudents.Count == 0)
            return BuildEmptyPage(input);

        var allStudentIds = allStudents.Select(s => s.Id).ToList();

        // ── 2. StudentFeeRecord lookup (studentFeeId) ─────────────────────
        var feeRecordQuery = await studentFeeRecordRepository.GetQueryableAsync();
        var feeRecordByStudent = feeRecordQuery
            .Where(r => allStudentIds.Contains(r.StudentId)
                     && r.AcademicYearId == input.AcademicYearId)
            .Select(r => new { r.StudentId, r.Id })
            .ToDictionary(r => r.StudentId, r => r.Id);

        // ── 3. Total fees per student from their assigned fee structure ────
        var sfsQuery = await studentFeeStructureRepository.GetQueryableAsync();
        var detailQuery = await feeStructureDetailRepository.GetQueryableAsync();

        // Map studentId → FeeStructureId (the per-standard structure)
        var structureByStudent = sfsQuery
            .Where(sfs => allStudentIds.Contains(sfs.StudentId))
            .Select(sfs => new { sfs.StudentId, sfs.FeeStructureStandardId })
            .ToDictionary(sfs => sfs.StudentId, sfs => sfs.FeeStructureStandardId);

        var usedStructureIds = structureByStudent.Values.Distinct().ToList();

        // Sum of FeeStructureDetail amounts keyed by FeeStructureId
        var totalByStructure = detailQuery
            .Where(d => usedStructureIds.Contains(d.FeeStructureId))
            .GroupBy(d => d.FeeStructureId)
            .Select(g => new { FeeStructureId = g.Key, Total = g.Sum(d => d.Amount) })
            .ToDictionary(x => x.FeeStructureId, x => x.Total);

        // Resolve per-student total
        var totalByStudent = allStudentIds.ToDictionary(
            sid => sid,
            sid => structureByStudent.TryGetValue(sid, out var fsId)
                ? totalByStructure.GetValueOrDefault(fsId, 0d)
                : 0d);

        // ── 4. Paid fees per student ───────────────────────────────────────
        var feeCollQuery = await feeCollectionRecordRepository.GetQueryableAsync();
        var paidByStudent = feeCollQuery
            .Where(r => allStudentIds.Contains(r.StudentId))
            .GroupBy(r => r.StudentId)
            .Select(g => new { StudentId = g.Key, Paid = g.Sum(r => r.Amount) })
            .ToDictionary(x => x.StudentId, x => x.Paid);

        // ── 5. Overdue fees — unpaid approved charges past their due date ────
        var now = DateTime.UtcNow;
        var chargesQuery = await chargeRepository.GetQueryableAsync();
        var overdueChargeList = chargesQuery
            .Where(c => allStudentIds.Contains(c.StudentId)
                     && c.AcademicYearId == input.AcademicYearId
                     && c.Status == ChargeStatus.Approved
                     && c.DueDate < now)
            .Select(c => new { c.Id, c.StudentId, Amount = (double)c.Amount })
            .ToList();

        var overdueByStudent = new Dictionary<Guid, double>();
        if (overdueChargeList.Count > 0)
        {
            var overdueIds = overdueChargeList.Select(c => c.Id).ToList();
            var chargeCollQuery = await chargeCollectionRecordRepository.GetQueryableAsync();
            var paidPerOverdueCharge = chargeCollQuery
                .Where(r => overdueIds.Contains(r.ChargeId))
                .GroupBy(r => r.ChargeId)
                .Select(g => new { ChargeId = g.Key, Paid = g.Sum(r => r.Amount) })
                .ToDictionary(x => x.ChargeId, x => x.Paid);

            overdueByStudent = overdueChargeList
                .GroupBy(c => c.StudentId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(c => Math.Max(
                        0d, c.Amount - paidPerOverdueCharge.GetValueOrDefault(c.Id, 0d))));
        }

        // ── 6. Project each student to a list item ─────────────────────────
        var allItems = allStudents.Select(s =>
        {
            var total   = totalByStudent.GetValueOrDefault(s.Id, 0d);
            var paid    = paidByStudent.GetValueOrDefault(s.Id, 0d);
            var pending = Math.Max(0d, total - paid);
            var overdue = overdueByStudent.GetValueOrDefault(s.Id, 0d);

            var status = pending <= 0d
                ? FeeCollectionStatus.Paid
                : overdue > 0d
                    ? FeeCollectionStatus.Overdue
                    : FeeCollectionStatus.Pending;

            return new FeeCollectionListItemDto
            {
                StudentFeeId = feeRecordByStudent.GetValueOrDefault(s.Id, Guid.Empty),
                StudentId    = s.Id,
                StudentName  = string.Join(" ",
                    new[] { s.FirstName, s.MiddleName, s.LastName }
                        .Where(n => !string.IsNullOrWhiteSpace(n))),
                StandardName = s.Standard?.Name  ?? string.Empty,
                DivisionName = s.Division?.Name  ?? string.Empty,
                RollNo       = s.RollNo?.ToString()  ?? string.Empty,
                GrNo         = s.GrNo?.ToString()    ?? string.Empty,
                TotalFees    = total,
                PaidFees     = paid,
                PendingFees  = pending,
                OverdueFees  = overdue,
                Status       = status,
            };
        }).ToList();

        // ── 7. Apply optional status filter, then paginate in memory ──────
        var filtered = input.Status.HasValue
            ? allItems.Where(i => i.Status == input.Status.Value).ToList()
            : allItems;

        var totalCount = filtered.Count;
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)input.PageSize);
        var pageItems  = filtered
            .Skip((input.Page - 1) * input.PageSize)
            .Take(input.PageSize)
            .ToList();

        return new StudentFeeCollectionListDto
        {
            TotalCount  = totalCount,
            CurrentPage = input.Page,
            PageSize    = input.PageSize,
            TotalPages  = totalPages,
            Items       = pageItems,
        };
    }

    // ── Private: empty-page helper ────────────────────────────────────────
    private static StudentFeeCollectionListDto BuildEmptyPage(GetStudentsInput input) =>
        new()
        {
            TotalCount  = 0,
            CurrentPage = input.Page,
            PageSize    = input.PageSize,
            TotalPages  = 0,
            Items       = [],
        };

    [Authorize(ShikshaMitraSubModulePermissions.FeeCollection)]
    public async Task<CollectFeeOutput> CollectFeeAsync(CollectFeeInput input)
    {
        // ── 1. Validate referenced entities ──────────────────────────────
        await academicYearRepository.GetAsync(input.AcademicYearId);

        var feeHeadIds = input.SelectedFeeHeads.Select(f => f.FeeHeadId).Distinct().ToList();
        var feeHeads = await feeHeadRepository.GetListAsync(fh => feeHeadIds.Contains(fh.Id));

        if (feeHeads.Count != feeHeadIds.Count)
            throw new BusinessException(FeeCollectionErrorCodes.InvalidFeeHead);

        var chargeMap = new Dictionary<Guid, Charge>();
        if (input.SelectedCharges is { Count: > 0 })
        {
            var chargeIds = input.SelectedCharges.Select(c => c.ChargeId).Distinct().ToList();
            var charges = await chargeRepository.GetListAsync(c => chargeIds.Contains(c.Id));
            if (charges.Count != chargeIds.Count)
                throw new BusinessException(FeeCollectionErrorCodes.InvalidCharge);
            chargeMap = charges.ToDictionary(c => c.Id);
        }

        // ── 2. Determine status ───────────────────────────────────────────
        var received = input.ReceivedAmount ?? input.TotalAmount;
        var status = received >= input.TotalAmount
            ? FeeCollectionStatus.Paid
            : FeeCollectionStatus.Pending;

        // ── 3. Generate receipt number ────────────────────────────────────
        var receiptNumber = await GenerateReceiptNumberAsync();

        // ── 4. Create FeeRecordMetadata (root of this collection event) ──
        var now = DateTimeOffset.UtcNow;
        var metadata = new FeeRecordMetadata(GuidGenerator.Create())
        {
            TenantId = CurrentTenant.Id,
            StudentId = input.StudentId,
            AcademicYearId = input.AcademicYearId,
            TotalPaidFee = received,
            ReceiptNumber = receiptNumber,
            Status = status,
            Remarks = input.Notes,
        };
        await feeRecordMetadataRepository.InsertAsync(metadata, autoSave: false);

        // ── 5. Fee-head collection records ────────────────────────────────
        var feeHeadMap = feeHeads.ToDictionary(fh => fh.Id);
        var feeCollectionRecords = input.SelectedFeeHeads
            .Select(sfh => new FeeCollectionRecord(GuidGenerator.Create())
            {
                TenantId = CurrentTenant.Id,
                StudentId = input.StudentId,
                FeeHeadId = sfh.FeeHeadId,
                Amount = sfh.Amount,
                PaymentDate = now,
                FeeRecordMetadataId = metadata.Id,
            })
            .ToList();
        await feeCollectionRecordRepository.InsertManyAsync(feeCollectionRecords, autoSave: false);

        // ── 6. Charge collection records ──────────────────────────────────
        var chargeCollectionRecords = new List<ChargeCollectionRecord>();
        if (input.SelectedCharges is { Count: > 0 })
        {
            chargeCollectionRecords = input.SelectedCharges
                .Select(sc => new ChargeCollectionRecord(GuidGenerator.Create())
                {
                    TenantId = CurrentTenant.Id,
                    StudentId = input.StudentId,
                    ChargeId = sc.ChargeId,
                    Amount = sc.Amount,
                    PaymentDate = now,
                    FeeRecordMetadataId = metadata.Id,
                })
                .ToList();
            await chargeCollectionRecordRepository.InsertManyAsync(chargeCollectionRecords, autoSave: false);
        }

        // ── 7. Payment-method records ─────────────────────────────────────
        var paymentRecords = input.Payments
            .Select(p => PaymentMethodRecord.Create(
                studentId: input.StudentId,
                feeRecordMetadataId: metadata.Id,
                amount: (decimal)p.Amount,
                paymentDate: now,
                paymentMethod: p.PaymentMethod,
                paymentDetails: MapPaymentDetails(p.PaymentMethod, p.Details),
                tenantId: CurrentTenant.Id,
                notes: input.Notes))
            .ToList();
        await paymentMethodRecordRepository.InsertManyAsync(paymentRecords, autoSave: false);

        // ── 8. Apply DueFee allocations (optional — backward-compatible) ──
        var allAllocations = input.SelectedFeeHeads
            .Where(sfh => sfh.Allocations is { Count: > 0 })
            .SelectMany(sfh => sfh.Allocations!)
            .ToList();

        if (allAllocations.Count > 0)
        {
            var dueFeeIds = allAllocations.Select(a => a.DueFeeId).Distinct().ToList();
            var dueFees = await dueFeeRepository.GetListAsync(df => dueFeeIds.Contains(df.Id));

            if (dueFees.Count != dueFeeIds.Count)
                throw new BusinessException(DueFeeErrorCodes.DueFeeNotFound);

            var dueFeeMap = dueFees.ToDictionary(df => df.Id);
            foreach (var alloc in allAllocations)
            {
                dueFeeMap[alloc.DueFeeId].ApplyPayment(alloc.Amount);
            }

            await dueFeeRepository.UpdateManyAsync(dueFees, autoSave: true);
        }
        else
        {
            // No explicit allocations — flush previous inserts without DueFee updates
            await CurrentUnitOfWork!.SaveChangesAsync();
        }

        // ── 9. Build and return response ──────────────────────────────────
        var collectedBy = currentUser.Id ?? Guid.Empty;

        return new CollectFeeOutput
        {
            Id = metadata.Id,
            ReceiptNumber = receiptNumber,
            StudentId = input.StudentId,
            AcademicYearId = input.AcademicYearId,
            TotalAmount = input.TotalAmount,
            Status = status,
            CollectedBy = collectedBy,
            CollectedAt = now,
            CreatedAt = now,
            ReceiptUrl = null,
            PaidFeeHeads = feeCollectionRecords
                .Select(r => new PaidFeeHeadInfoDto
                {
                    FeeHeadId = r.FeeHeadId,
                    FeeHeadName = feeHeadMap[r.FeeHeadId].Name,
                    AmountPaid = r.Amount,
                })
                .ToList(),
            PaidCharges = chargeCollectionRecords.Count > 0
                ? chargeCollectionRecords
                    .Select(r => new PaidChargeInfoDto
                    {
                        ChargeId = r.ChargeId,
                        ChargeName = chargeMap[r.ChargeId].PurposeDescription,
                        AmountPaid = r.Amount,
                    })
                    .ToList()
                : null,
        };
    }

    [Authorize(ShikshaMitraSubModulePermissions.FeeCollection)]
    public async Task<StudentFeeDetailsDto> GetStudentFeeDetailsAsync(GetStudentFeeDetailsInput input)
    {
        // ── 1. Load student (navigation props auto-included via EF config) ─
        var student = await studentRepository.GetAsync(input.StudentId);

        // ── 2. Build student summary ──────────────────────────────────────
        var studentSummary = new StudentSummaryDto
        {
            Id           = student.Id,
            Name         = string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }
                               .Where(n => !string.IsNullOrWhiteSpace(n))),
            StandardName = student.Standard?.Name ?? string.Empty,
            DivisionName = student.Division?.Name ?? string.Empty,
            RollNo       = student.RollNo?.ToString() ?? string.Empty,
            MobileNo     = student.ContactDetail?.PrimaryContactNo,
        };

        // ── 3. Resolve fee structure for fee head definitions ─────────────
        var feeHeadSummaries = new List<FeeHeadSummaryDto>();

        var feeStructureAssignment = await studentFeeStructureRepository
            .FirstOrDefaultAsync(x => x.StudentId == input.StudentId);

        if (feeStructureAssignment != null)
        {
            var feeStructure = await feeStructureRepository
                .GetAsync(feeStructureAssignment.FeeStructureStandardId);

            var detailsQuery    = await feeStructureDetailRepository.GetQueryableAsync();
            var feeHeadsQuery   = await feeHeadRepository.GetQueryableAsync();
            var categoriesQuery = await feeCategoryRepository.GetQueryableAsync();

            var feeHeadDefs = (from detail in detailsQuery
                               join fh  in feeHeadsQuery   on detail.FeeHeadId    equals fh.Id
                               where detail.FeeStructureId == feeStructureAssignment.FeeStructureStandardId
                               select new
                               {
                                   detail.FeeHeadId,
                                   FeeHeadName = fh.Name,
                                   Description = fh.Description ?? string.Empty,
                                   NetAmount   = detail.Amount,
                               }).ToList();

            // Paid per fee head across all collection events for this student
            var feeCollQuery = await feeCollectionRecordRepository.GetQueryableAsync();
            var paidPerHead = feeCollQuery
                .Where(r => r.StudentId == input.StudentId)
                .GroupBy(r => r.FeeHeadId)
                .Select(g => new { FeeHeadId = g.Key, Paid = g.Sum(r => r.Amount) })
                .ToDictionary(x => x.FeeHeadId, x => x.Paid);

            feeHeadSummaries = feeHeadDefs.Select(def =>
            {
                var paid    = paidPerHead.GetValueOrDefault(def.FeeHeadId, 0d);
                var pending = Math.Max(0d, def.NetAmount - paid);
                return new FeeHeadSummaryDto
                {
                    FeeHeadId     = def.FeeHeadId,
                    FeeHeadName   = def.FeeHeadName,
                    Amount        = def.NetAmount,
                    PaidAmount    = paid,
                    PendingAmount = pending,
                    Description   = def.Description,
                    Status        = pending <= 0d ? FeeCollectionStatus.Paid : FeeCollectionStatus.Pending,
                };
            }).ToList();
        }

        // ── 4. Charges for this student in this academic year (approved only) ─
        var chargesQuery = await chargeRepository.GetQueryableAsync();
        var studentCharges = chargesQuery
            .Where(c => c.StudentId == input.StudentId && c.AcademicYearId == input.AcademicYearId && c.Status == ChargeStatus.Approved)
            .ToList();

        List<FeeChargeDetailDto>? chargeDtos = null;
        if (studentCharges.Count > 0)
        {
            var chargeCollQuery = await chargeCollectionRecordRepository.GetQueryableAsync();
            var paidPerCharge = chargeCollQuery
                .Where(r => r.StudentId == input.StudentId)
                .GroupBy(r => r.ChargeId)
                .Select(g => new { ChargeId = g.Key, Paid = g.Sum(r => r.Amount) })
                .ToDictionary(x => x.ChargeId, x => x.Paid);

            chargeDtos = studentCharges.Select(charge =>
            {
                var net     = (double)charge.Amount;
                var paid    = paidPerCharge.GetValueOrDefault(charge.Id, 0d);
                var pending = Math.Max(0d, net - paid);
                return new FeeChargeDetailDto
                {
                    ChargeId      = charge.Id,
                    Name          = charge.PurposeDescription,
                    Amount        = net,
                    PaidAmount    = paid,
                    PendingAmount = pending,
                    Status        = pending <= 0d ? FeeCollectionStatus.Paid : FeeCollectionStatus.Pending,
                };
            }).ToList();
        }

        // ── 5. Payment history ────────────────────────────────────────────
        var metadataQuery = await feeRecordMetadataRepository.GetQueryableAsync();
        var paymentRecordsQuery = await paymentMethodRecordRepository.GetQueryableAsync();

        var metadataForStudent = metadataQuery
            .Where(m => m.StudentId == input.StudentId && m.AcademicYearId == input.AcademicYearId)
            .OrderByDescending(m => m.CreationTime)
            .ToList();

        List<FeePaymentHistoryDto>? payments = null;
        if (metadataForStudent.Count > 0)
        {
            var metadataIds = metadataForStudent.Select(m => m.Id).ToList();

            var methodsByMetadata = paymentRecordsQuery
                .Where(p => metadataIds.Contains(p.FeeRecordMetadataId))
                .GroupBy(p => p.FeeRecordMetadataId)
                .Select(g => new
                {
                    MetadataId     = g.Key,
                    PaymentMethods = g.Select(p => p.PaymentMethod).ToList(),
                })
                .ToDictionary(x => x.MetadataId, x => x.PaymentMethods);

            payments = metadataForStudent.Select(m => new FeePaymentHistoryDto
            {
                PaymentId      = m.Id,
                ReceiptNumber  = m.ReceiptNumber,
                Date           = DateOnly.FromDateTime(m.CreationTime.Date),
                Amount         = m.TotalPaidFee,
                PaymentMethods = methodsByMetadata.TryGetValue(m.Id, out var methods)
                    ? methods
                    : [],
                ReceiptUrl     = null,
            }).ToList();
        }

        // ── 6. Concessions (approved requests only) ───────────────────────
        var concessionRequests = await concessionRequestRepository.GetListAsync(
            skipCount : 0,
            maxResultCount : int.MaxValue,
            studentId : input.StudentId,
            status    : ConcessionRequestStatus.Approved);

        List<ConcessionDetailDto>? concessions = null;
        if (concessionRequests.Count > 0)
        {
            var typeIds = concessionRequests.Select(r => r.ConcessionTypeId).Distinct().ToList();
            var concessionTypes = await concessionTypeRepository
                .GetListAsync(ct => typeIds.Contains(ct.Id));
            var typeMap = concessionTypes.ToDictionary(ct => ct.Id, ct => ct.Name);

            concessions = concessionRequests.Select(r =>
            {
                var totalConcession = r.ConcessionFeeHeadMappings
                    .Sum(m => (double)m.FeeHeadConcessionAmount);
                return new ConcessionDetailDto
                {
                    ConcessionRequestConcessionTypeId = r.Id,
                    ConcessionTypeName = typeMap.GetValueOrDefault(r.ConcessionTypeId, string.Empty),
                    Amount = totalConcession > 0d ? totalConcession : null,
                };
            }).ToList();
        }

        // ── 7. Fee overview ───────────────────────────────────────────────
        var totalFee       = feeHeadSummaries.Sum(fh => fh.Amount);
        var totalPaid      = feeHeadSummaries.Sum(fh => fh.PaidAmount);
        var totalPending   = feeHeadSummaries.Sum(fh => fh.PendingAmount ?? 0d);
        var totalConcession = concessions?.Sum(c => c.Amount ?? 0d);

        return new StudentFeeDetailsDto
        {
            Student     = studentSummary,
            FeeOverview = new FeeOverviewDto
            {
                TotalFee         = totalFee,
                PaidFee          = totalPaid,
                PendingFee       = totalPending,
                ConcessionAmount = totalConcession > 0d ? totalConcession : null,
            },
            FeeHeads    = feeHeadSummaries,
            Charges     = chargeDtos,
            Payments    = payments,
            Concessions = concessions,
        };
    }

    public Task<ComprehensiveFeeDetailsDto> GetComprehensiveFeeDetailsAsync(GetComprehensiveFeeDetailsInput input)
    {
        throw new NotImplementedException();
    }

    [Authorize(ShikshaMitraSubModulePermissions.FeeCollection)]
    public async Task<FeeCollectionDetailsDto> GetFeeCollectionDetailsAsync(Guid studentFeeId)
    {
        // ── 1. Resolve the student fee record ─────────────────────────────
        var studentFeeRecord = await studentFeeRecordRepository.FindAsync(studentFeeId)
            ?? throw new BusinessException(FeeCollectionErrorCodes.InvalidFeeHead)
                .WithData("StudentFeeId", studentFeeId);

        var studentId = studentFeeRecord.StudentId;
        var academicYearId = studentFeeRecord.AcademicYearId;

        // ── 2. Load student (navigation props auto-included via EF config) ─
        var student = await studentRepository.GetAsync(studentId);

        // ── 3. Resolve fee structure assignment ───────────────────────────
        var feeStructureAssignment = await studentFeeStructureRepository
            .FirstOrDefaultAsync(x => x.StudentId == studentId);

        var isFeeStructureAssigned = feeStructureAssignment != null;
        var feeStructureName = string.Empty;
        var feeHeadDetails = new List<FeeHeadDetailDto>();

        if (isFeeStructureAssigned)
        {
            var feeStructure = await feeStructureRepository
                .GetAsync(feeStructureAssignment!.FeeStructureStandardId);

            feeStructureName = feeStructure.Name;

            // ── 4. Load fee head definitions from the structure ───────────
            var detailsQuery   = await feeStructureDetailRepository.GetQueryableAsync();
            var feeHeadsQuery  = await feeHeadRepository.GetQueryableAsync();
            var categoriesQuery = await feeCategoryRepository.GetQueryableAsync();

            var feeHeadDefs = (from detail in detailsQuery
                               join fh  in feeHeadsQuery   on detail.FeeHeadId    equals fh.Id
                               join cat in categoriesQuery on fh.FeeCategoryId equals cat.Id
                               where detail.FeeStructureId == feeStructureAssignment.FeeStructureStandardId
                               select new
                               {
                                   detail.FeeHeadId,
                                   FeeHeadName  = fh.Name,
                                   CategoryName = cat.Name,
                                   NetAmount    = detail.Amount,
                                   detail.IsOneTime,
                                   detail.IsOptional,
                               }).ToList();

            // ── 5. Compute paid amounts per fee head ───────────────────────
            var feeCollRecordsQuery = await feeCollectionRecordRepository.GetQueryableAsync();
            var paidPerHead = feeCollRecordsQuery
                .Where(r => r.StudentId == studentId)
                .GroupBy(r => r.FeeHeadId)
                .Select(g => new { FeeHeadId = g.Key, Paid = g.Sum(r => r.Amount) })
                .ToDictionary(x => x.FeeHeadId, x => x.Paid);

            feeHeadDetails = feeHeadDefs.Select(def =>
            {
                var paid    = paidPerHead.GetValueOrDefault(def.FeeHeadId, 0d);
                var pending = Math.Max(0d, def.NetAmount - paid);
                return new FeeHeadDetailDto
                {
                    FeeHeadId    = def.FeeHeadId,
                    FeeHeadName  = def.FeeHeadName,
                    CategoryName = def.CategoryName,
                    NetAmount    = def.NetAmount,
                    PaidAmount   = paid,
                    PendingAmount = pending,
                    Status       = pending <= 0d ? FeeCollectionStatus.Paid : FeeCollectionStatus.Pending,
                    IsSelected   = true,
                    IsOneTime    = def.IsOneTime,
                    IsOptional   = def.IsOptional,
                    Charges      = null,
                };
            }).ToList();
        }

        // ── 6. Resolve charges for the student in this academic year ──────
        var chargesQuery = await chargeRepository.GetQueryableAsync();
        var studentCharges = chargesQuery
            .Where(c => c.StudentId == studentId && c.AcademicYearId == academicYearId)
            .ToList();

        List<ChargeDetailDto>? chargeDtos = null;
        if (studentCharges.Count > 0)
        {
            var chargeCollRecordsQuery = await chargeCollectionRecordRepository.GetQueryableAsync();
            var paidPerCharge = chargeCollRecordsQuery
                .Where(r => r.StudentId == studentId)
                .GroupBy(r => r.ChargeId)
                .Select(g => new { ChargeId = g.Key, Paid = g.Sum(r => r.Amount) })
                .ToDictionary(x => x.ChargeId, x => x.Paid);

            chargeDtos = studentCharges.Select(charge =>
            {
                var netAmount = (double)charge.Amount;
                var paid      = paidPerCharge.GetValueOrDefault(charge.Id, 0d);
                var pending   = Math.Max(0d, netAmount - paid);
                return new ChargeDetailDto
                {
                    ChargeId      = charge.Id,
                    Name          = charge.PurposeDescription,
                    Amount        = netAmount,
                    PaidAmount    = paid,
                    PendingAmount = pending,
                    Status        = pending <= 0d ? FeeCollectionStatus.Paid : FeeCollectionStatus.Pending,
                };
            }).ToList();
        }

        // ── 7. Compute fee summary ─────────────────────────────────────────
        var totalFee  = feeHeadDetails.Sum(fh => fh.NetAmount);
        var paidFee   = feeHeadDetails.Sum(fh => fh.PaidAmount);
        var pendingFee = feeHeadDetails.Sum(fh => fh.PendingAmount);

        // ── 8. Build student info ─────────────────────────────────────────
        var studentInfo = new StudentInfoDto
        {
            StudentId            = student.Id,
            StudentName          = string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }
                                       .Where(n => !string.IsNullOrWhiteSpace(n))),
            GrNo                 = student.GrNo?.ToString() ?? string.Empty,
            RollNo               = student.RollNo?.ToString() ?? string.Empty,
            BoardId              = student.AcademicBoardId ?? Guid.Empty,
            BoardName            = student.AcademicBoard?.Type.ToString() ?? string.Empty,
            EducationMediumId    = student.AcademicMediumId,
            EducationMediumName  = student.AcademicMedium?.Name ?? string.Empty,
            StandardId           = student.StandardId ?? Guid.Empty,
            StandardName         = student.Standard?.Name ?? string.Empty,
            DivisionId           = student.DivisionId ?? Guid.Empty,
            DivisionName         = student.Division?.Name ?? string.Empty,
            ContactNo            = student.ContactDetail?.PrimaryContactNo ?? string.Empty,
            Email                = student.ContactDetail?.Email ?? string.Empty,
            FeeStructureName     = feeStructureName,
            IsFeeStructureAssigned = isFeeStructureAssigned,
        };

        return new FeeCollectionDetailsDto
        {
            Student  = studentInfo,
            Summary  = new FeeSummaryDto
            {
                TotalFee   = totalFee,
                PaidFee    = paidFee,
                PendingFee = pendingFee,
            },
            FeeHeads = feeHeadDetails,
            Charges  = chargeDtos,
        };
    }

    // ── Private helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Generates a unique receipt number in the format REC-{YYYY}-{NNNNNN}.
    /// Uses the total count of existing metadata records to derive the sequence.
    /// </summary>
    private async Task<string> GenerateReceiptNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var count = await feeRecordMetadataRepository.CountAsync();
        var sequence = (count + 1).ToString("D6");
        return $"REC-{year}-{sequence}";
    }

    /// <summary>
    /// Maps the raw details dictionary from the API request into a strongly-typed
    /// <see cref="PaymentDetails.PaymentDetails"/> value object matching the payment method.
    /// Returns <c>null</c> for Cash — no instrument details are required.
    /// </summary>
    private static PaymentDetails? MapPaymentDetails(
        PaymentMethodEnum method,
        Dictionary<string, object?>? details)
    {
        if (method == PaymentMethodEnum.Cash)
            return null;

        details ??= [];

        string Get(string key) =>
            details.TryGetValue(key, out var v) ? v?.ToString() ?? string.Empty : string.Empty;

        return method switch
        {
            PaymentMethodEnum.Cheque => new ChequeDetails(
                chequeNumber: Get("chequeNumber"),
                bankName: Get("bankName"),
                branchName: Get("branchName"),
                chequeDate: DateOnly.TryParse(Get("chequeDate"), out var cd)
                    ? cd : DateOnly.FromDateTime(DateTime.UtcNow)),

            PaymentMethodEnum.DemandDraft => new DemandDraftDetails(
                ddNumber: Get("ddNumber"),
                issuingBankName: Get("issuingBankName"),
                branchName: Get("branchName"),
                ddDate: DateOnly.TryParse(Get("ddDate"), out var dd)
                    ? dd : DateOnly.FromDateTime(DateTime.UtcNow)),

            PaymentMethodEnum.BankTransfer => new BankTransferDetails(
                transactionReferenceNumber: Get("transactionReferenceNumber"),
                bankName: Get("bankName"),
                ifscCode: Get("ifscCode")),

            PaymentMethodEnum.UPI => new UpiDetails(
                upiTransactionId: Get("upiTransactionId"),
                upiId: Get("upiId"),
                appName: Get("appName")),

            // CreditCard, OnlinePaymentGateway, WalletPrepaidCard — no dedicated
            // details type yet. Domain validation in PaymentMethodRecord.Create()
            // will throw if details are unexpectedly required.
            _ => null,
        };
    }
}

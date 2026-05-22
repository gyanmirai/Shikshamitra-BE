using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.FeeRefund;
using ShikshaMitra.Finance.FeeRefund.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Volo.Abp.Users;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

[Authorize]
[RemoteService(false)]
public class FeeRefundAppService(
    IFeeRefundRepository feeRefundRepository,
    IDueFeeRepository dueFeeRepository,
    IRepository<FeeHead, Guid> feeHeadRepository,
    IRepository<Student, Guid> studentRepository,
    FeeRefundManager feeRefundManager,
    ICurrentUser currentUser,
    IUnitOfWorkManager unitOfWorkManager
) : ApplicationService, IFeeRefundAppService
{
    // ── List ─────────────────────────────────────────────────────────────────

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View)]
    public async Task<PagedResultDto<FeeRefundRequestDto>> GetListAsync(GetFeeRefundsInput input)
    {
        var (items, totalCount) = await feeRefundRepository.GetListWithDetailsAsync(
            studentId: input.StudentId,
            academicMediumId: input.AcademicMediumId,
            status: input.Status,
            search: input.Search,
            skipCount: input.SkipCount,
            maxResultCount: input.MaxResultCount,
            sorting: input.Sorting);

        var studentMap = await BuildStudentMapAsync(items.Select(r => r.StudentId).Distinct());
        var dtos = items.Select(r => MapToDto(r, studentMap)).ToList();
        return new PagedResultDto<FeeRefundRequestDto>(totalCount, dtos);
    }

    // ── Get single ───────────────────────────────────────────────────────────

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View)]
    public async Task<FeeRefundRequestDto> GetAsync(Guid id)
    {
        var request = await feeRefundRepository.FindWithLinesAsync(id)
            ?? throw new BusinessException(FeeRefundErrorCodes.RefundNotFound)
                .WithData("Id", id);

        var studentMap = await BuildStudentMapAsync([request.StudentId]);
        return MapToDto(request, studentMap);
    }

    // ── Eligibility ──────────────────────────────────────────────────────────

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.View)]
    public async Task<RefundEligibilityDto> GetEligibilityAsync(Guid studentId, Guid academicYearId)
    {
        var dueFees = await dueFeeRepository.GetListAsync(
            df => df.StudentId == studentId && df.PaidAmount > 0);

        if (dueFees.Count == 0)
            return new RefundEligibilityDto
            {
                StudentId = studentId,
                AcademicYearId = academicYearId,
            };

        var feeHeadIds = dueFees.Select(df => df.FeeHeadId).Distinct().ToList();
        var feeHeads = await feeHeadRepository.GetListAsync(fh => feeHeadIds.Contains(fh.Id));
        var feeHeadMap = feeHeads.ToDictionary(fh => fh.Id, fh => fh.Name);

        var grouped = dueFees
            .GroupBy(df => df.FeeHeadId)
            .Select(g =>
            {
                var months = g
                    .OrderBy(df => df.Year)
                    .ThenBy(df => df.Month)
                    .Select(df => new RefundEligibilityMonthDto
                    {
                        DueFeeId   = df.Id,
                        Month      = df.Month,
                        Year       = df.Year,
                        Total      = df.OriginalAmount,
                        Paid       = df.PaidAmount,
                        Refunded   = df.RefundedAmount,
                        Refundable = df.RefundableBalance,
                    })
                    .ToList();

                return new RefundEligibilityFeeHeadDto
                {
                    FeeHeadId       = g.Key,
                    FeeHeadName     = feeHeadMap.TryGetValue(g.Key, out var n) ? n : g.Key.ToString(),
                    TotalPaid       = g.Sum(df => df.PaidAmount),
                    TotalRefunded   = g.Sum(df => df.RefundedAmount),
                    TotalRefundable = g.Sum(df => df.RefundableBalance),
                    Months          = months,
                };
            })
            .ToList();

        return new RefundEligibilityDto
        {
            StudentId       = studentId,
            AcademicYearId  = academicYearId,
            TotalPaid       = grouped.Sum(fh => fh.TotalPaid),
            TotalRefunded   = grouped.Sum(fh => fh.TotalRefunded),
            TotalRefundable = grouped.Sum(fh => fh.TotalRefundable),
            FeeHeads        = grouped,
        };
    }

    // ── Create ───────────────────────────────────────────────────────────────

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Add)]
    public async Task<FeeRefundRequestDto> CreateAsync(CreateFeeRefundRequestDto input)
    {
        using var uow = unitOfWorkManager.Begin(isTransactional: true);

        var lines = input.Lines
            .Select(l => (l.DueFeeId, l.Amount))
            .ToList();

        var request = await feeRefundManager.CreateAsync(
            studentId: input.StudentId,
            academicYearId: input.AcademicYearId,
            lines: lines,
            refundMethod: input.RefundMethod,
            reason: input.Reason);

        await uow.CompleteAsync();

        var studentMap = await BuildStudentMapAsync([request.StudentId]);
        return MapToDto(request, studentMap);
    }

    // ── Status transition ────────────────────────────────────────────────────

    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Approve)]
    public async Task<FeeRefundRequestDto> UpdateStatusAsync(Guid id, UpdateFeeRefundStatusDto input)
    {
        var request = await feeRefundRepository.FindWithLinesAsync(id)
            ?? throw new BusinessException(FeeRefundErrorCodes.RefundNotFound)
                .WithData("Id", id);

        using var uow = unitOfWorkManager.Begin(isTransactional: true);

        var userId = currentUser.GetId();

        switch (input.Status)
        {
            case FeeRefundStatus.UnderReview:
                request.MarkUnderReview();
                break;

            case FeeRefundStatus.Approved:
                await feeRefundManager.ApproveAsync(request, userId, input.Notes);
                break;

            case FeeRefundStatus.Rejected:
                request.Reject(userId, input.Notes);
                break;

            case FeeRefundStatus.Cancelled:
                request.Cancel();
                break;

            default:
                throw new BusinessException(FeeRefundErrorCodes.IllegalStatusTransition)
                    .WithData("TargetStatus", input.Status);
        }

        await feeRefundRepository.UpdateAsync(request, autoSave: false);
        await uow.CompleteAsync();

        var studentMap = await BuildStudentMapAsync([request.StudentId]);
        return MapToDto(request, studentMap);
    }

    // ── Student snapshot helper ───────────────────────────────────────────────

    /// <summary>
    /// Loads students for the given IDs in a single batch query.
    /// StudentConfiguration.AutoInclude() ensures ContactDetail, Standard, and Division
    /// are always loaded together with the Student — no extra joins required.
    /// </summary>
    private async Task<Dictionary<Guid, Student>> BuildStudentMapAsync(IEnumerable<Guid> studentIds)
    {
        var ids = studentIds.ToList();
        if (ids.Count == 0) return [];
        var students = await studentRepository.GetListAsync(s => ids.Contains(s.Id));
        return students.ToDictionary(s => s.Id);
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static FeeRefundRequestDto MapToDto(FeeRefundRequest r, Dictionary<Guid, Student> studentMap)
    {
        studentMap.TryGetValue(r.StudentId, out var student);

        var fullName = student is null
            ? string.Empty
            : string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }
                .Where(p => !string.IsNullOrWhiteSpace(p)));

        return new FeeRefundRequestDto
        {
            Id                = r.Id,
            StudentId         = r.StudentId,
            AcademicYearId    = r.AcademicYearId,
            Status            = r.Status,
            VoucherNumber     = r.VoucherNumber,
            TotalAmount       = r.TotalAmount,
            RefundMethod      = r.RefundMethod,
            Reason            = r.Reason,
            ApproverNotes     = r.ApproverNotes,
            ProcessedByUserId = r.ProcessedByUserId,
            ProcessedAt       = r.ProcessedAt,
            CreationTime      = r.CreationTime,
            Lines             = r.Lines
                .Select(l => new FeeRefundLineDto
                {
                    Id       = l.Id,
                    DueFeeId = l.DueFeeId,
                    Amount   = l.Amount,
                })
                .ToList(),

            // Student snapshot — null-safe; falls back to empty/null when student not found
            StudentName      = fullName,
            StudentGrNo      = student?.GrNo,
            StudentRollNo    = student?.RollNo,
            ClassName        = student?.Standard?.Name,
            DivisionName     = student?.Division?.Name,
            StudentContactNo = student?.ContactDetail?.PrimaryContactNo,
        };
    }
}

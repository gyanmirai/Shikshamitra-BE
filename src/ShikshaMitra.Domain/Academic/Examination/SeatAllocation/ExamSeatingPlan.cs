using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Examination.SeatAllocation;

/// <summary>
/// One draft/finalized seating plan per exam. Wraps the existing per-seat
/// <see cref="ExamSeatAllocation"/> rows with plan-level state (rooms in scope,
/// allocation rules, status, finalize/publish/lock bookkeeping) so the workspace
/// has a single place to persist draft configuration between sessions.
/// </summary>
public class ExamSeatingPlan : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid ExamId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public SeatingPlanScopeType ScopeType { get; private set; }
    public SeatingPlanStatus Status { get; private set; }
    public string? SessionKey { get; private set; }

    /// <summary>Comma-separated room ids currently in scope for this plan.</summary>
    public string RoomIdsCsv { get; private set; } = string.Empty;

    /// <summary>Serialized <c>SeatingAllocationRules</c> (strategy, fill direction, gap, mix divisions, special priority).</summary>
    public string AllocationRulesJson { get; private set; } = string.Empty;

    /// <summary>Comma-separated student ids excluded from this plan (absent/opted-out).</summary>
    public string ExcludedStudentIdsCsv { get; private set; } = string.Empty;

    /// <summary>
    /// Comma-separated student ids flagged as needing special seating (spec §17: front seat,
    /// accessible seat, extra space, etc.). Deliberately a flag only — no reason/detail is stored
    /// here, so nothing sensitive is exposed beyond "this student needs special seating".
    /// </summary>
    public string SpecialRequirementStudentIdsCsv { get; private set; } = string.Empty;

    public bool IsLocked { get; private set; }
    public DateTime? FinalizedAt { get; private set; }
    public Guid? FinalizedByUserId { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public Guid? PublishedByUserId { get; private set; }

    private ExamSeatingPlan() { }

    public ExamSeatingPlan(
        Guid id,
        Guid? tenantId,
        Guid examId,
        string name,
        SeatingPlanScopeType scopeType,
        string? sessionKey = null) : base(id)
    {
        TenantId = tenantId;
        ExamId = examId;
        Name = name;
        ScopeType = scopeType;
        SessionKey = sessionKey;
        Status = SeatingPlanStatus.NotStarted;
    }

    public void AssignSessionKey(string sessionKey)
    {
        if (string.IsNullOrWhiteSpace(sessionKey))
            throw new ArgumentException("Session key is required.", nameof(sessionKey));
        if (SessionKey != null && !string.Equals(SessionKey, sessionKey, StringComparison.Ordinal))
            throw new InvalidOperationException("A seating plan cannot be moved to another examination session.");
        SessionKey = sessionKey;
    }

    public void UpdateScope(string name, SeatingPlanScopeType scopeType)
    {
        if (IsLocked)
            throw new InvalidOperationException("Cannot modify a finalized seating plan without unlocking it first.");

        Name = name;
        ScopeType = scopeType;
    }

    public void SaveDraft(
        string roomIdsCsv,
        string allocationRulesJson,
        string excludedStudentIdsCsv,
        string specialRequirementStudentIdsCsv,
        SeatingPlanStatus status)
    {
        if (IsLocked)
            throw new InvalidOperationException("Cannot modify a finalized seating plan without unlocking it first.");

        RoomIdsCsv = roomIdsCsv;
        AllocationRulesJson = allocationRulesJson;
        ExcludedStudentIdsCsv = excludedStudentIdsCsv;
        SpecialRequirementStudentIdsCsv = specialRequirementStudentIdsCsv;
        Status = status;
    }

    public void Finalize(Guid userId, DateTime now)
    {
        IsLocked = true;
        Status = SeatingPlanStatus.Completed;
        FinalizedAt = now;
        FinalizedByUserId = userId;
    }

    public void Publish(Guid userId, DateTime now)
    {
        Status = SeatingPlanStatus.Published;
        PublishedAt = now;
        PublishedByUserId = userId;
    }

    public void Unlock()
    {
        IsLocked = false;
        Status = SeatingPlanStatus.Completed;
        FinalizedAt = null;
        FinalizedByUserId = null;
    }

    /// <summary>
    /// Reopens a legacy plan that was finalized or published before examination-wide
    /// publication existed. The application service may call this only while the
    /// examination has no <see cref="ExamSeatingPublication"/> boundary.
    /// </summary>
    public void ReopenDraft()
    {
        IsLocked = false;
        Status = SeatingPlanStatus.Completed;
        FinalizedAt = null;
        FinalizedByUserId = null;
        PublishedAt = null;
        PublishedByUserId = null;
    }
}

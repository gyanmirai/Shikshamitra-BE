using System;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Books;

public class LibraryCharge : AuditedAggregateRoot<Guid>, IMultiTenant
{
    private const string BoundAssessmentVersionProperty = "FinanceBoundAssessmentVersion";
    public Guid? TenantId { get; private set; }

    public Guid MemberId { get; set; }

    /// <summary>
    /// 0 = Student, 1 = Employee/Teacher
    /// </summary>
    public int MemberRole { get; set; }

    public Guid ChargeCategoryId { get; set; }

    public Guid? BookIssueId { get; set; }

    public decimal Amount { get; private set; }

    public DateTime? ChargeDate { get; private set; }

    public DateTime? DueDate { get; private set; }

    public DateOnly? SchoolChargeDate { get; private set; }
    public DateOnly? SchoolDueDate { get; private set; }
    public LibraryCivilDateProvenance ChargeDateProvenance { get; private set; }
    public LibraryCivilDateProvenance DueDateProvenance { get; private set; }
    public Guid? AcademicYearId { get; private set; }
    public LibraryAcademicYearProvenance AcademicYearProvenance { get; private set; }
    public string? AcademicYearEvidenceJson { get; private set; }
    public string? SourceDateEvidenceJson { get; private set; }
    public string? Currency { get; private set; }
    public Guid? FinanceSourceLinkId { get; private set; }

    public ChargePaymentStatus PaymentStatus { get; private set; }

    public DateTime? PaidDate { get; private set; }

    public string? Remarks { get; set; }

    protected LibraryCharge() { }
    private LibraryCharge(Guid id) : base(id) { }

    public LibraryCharge(
        Guid id,
        Guid memberId,
        int memberRole,
        Guid chargeCategoryId,
        Guid? bookIssueId,
        decimal amount,
        DateTime chargeDate,
        DateTime dueDate,
        Guid? tenantId = null)
        : base(id)
    {
        ValidateAmount(amount);
        ValidateDates(chargeDate, dueDate);
        TenantId = tenantId;
        MemberId = memberId;
        MemberRole = memberRole;
        ChargeCategoryId = chargeCategoryId;
        BookIssueId = bookIssueId;
        Amount = amount;
        ChargeDate = chargeDate;
        DueDate = dueDate;
        PaymentStatus = ChargePaymentStatus.Pending;
    }

    public void ApplyUpdate(decimal? amount, DateTime? dueDate, ChargePaymentStatus? paymentStatus,
        DateTime? paidDate, string? remarks, DateTime recordedAt, DateOnly? schoolDueDate = null,
        string? dateEvidenceJson = null)
    {
        EnsureCanAmend(paymentStatus, paidDate);
        var nextAmount = amount ?? Amount;
        var nextDueDate = dueDate ?? DueDate;
        ValidateAmount(nextAmount);
        if (dueDate.HasValue && SchoolDueDate.HasValue)
            throw new BusinessException(LibraryFinanceErrorCodes.SourceRequiresReview,
                "Use the assessment's school-date fields when changing a confirmed due date.");
        if (schoolDueDate.HasValue && (dueDate.HasValue || !SchoolChargeDate.HasValue || string.IsNullOrWhiteSpace(dateEvidenceJson)))
            throw new BusinessException(LibraryFinanceErrorCodes.SourceRequiresReview,
                "Review the assessment's school dates before changing its due date.");
        var nextSchoolDueDate = schoolDueDate ?? SchoolDueDate;
        if (SchoolChargeDate.HasValue && nextSchoolDueDate.HasValue)
            ValidateSchoolDates(SchoolChargeDate.Value, nextSchoolDueDate.Value);
        else
            ValidateDates(ChargeDate ?? default, nextDueDate ?? default);
        if (remarks?.Length > 500) throw Invalid("Charge remarks cannot exceed 500 characters.");

        Amount = nextAmount;
        DueDate = nextDueDate;
        if (schoolDueDate.HasValue)
        {
            SchoolDueDate = schoolDueDate;
            DueDateProvenance = LibraryCivilDateProvenance.ExplicitSchoolDate;
            SourceDateEvidenceJson = dateEvidenceJson;
        }
        if (remarks != null) Remarks = remarks.Trim();
    }

    public static LibraryCharge CreateConfirmed(Guid id, Guid memberId, int memberRole, Guid chargeCategoryId,
        Guid? issueId, decimal amount, DateOnly chargeDate, DateOnly dueDate, Guid academicYearId,
        LibraryAcademicYearProvenance yearProvenance, string yearEvidenceJson, string dateEvidenceJson,
        string? remarks, Guid tenantId)
    {
        ValidateAmount(amount);
        var charge = new LibraryCharge(id)
        {
            TenantId = tenantId, MemberId = memberId, MemberRole = memberRole,
            ChargeCategoryId = chargeCategoryId, BookIssueId = issueId, Amount = amount,
            PaymentStatus = ChargePaymentStatus.Pending, Remarks = remarks?.Trim()
        };
        charge.ConfirmSourceContext(academicYearId, yearProvenance, yearEvidenceJson, chargeDate, dueDate,
            LibraryCivilDateProvenance.ExplicitSchoolDate, LibraryCivilDateProvenance.ExplicitSchoolDate,
            dateEvidenceJson, "INR");
        return charge;
    }

    public void ConfirmSourceContext(Guid academicYearId, LibraryAcademicYearProvenance yearProvenance,
        string yearEvidenceJson, DateOnly chargeDate, DateOnly dueDate,
        LibraryCivilDateProvenance chargeProvenance, LibraryCivilDateProvenance dueProvenance,
        string dateEvidenceJson, string currency)
    {
        EnsureCanAmend();
        ValidateSchoolDates(chargeDate, dueDate);
        if (academicYearId == Guid.Empty || yearProvenance == LibraryAcademicYearProvenance.Unconfirmed ||
            !Enum.IsDefined(yearProvenance) || string.IsNullOrWhiteSpace(yearEvidenceJson) ||
            string.IsNullOrWhiteSpace(dateEvidenceJson) ||
            chargeProvenance == LibraryCivilDateProvenance.UnconfirmedStoredComponent ||
            dueProvenance == LibraryCivilDateProvenance.UnconfirmedStoredComponent ||
            !Enum.IsDefined(chargeProvenance) || !Enum.IsDefined(dueProvenance) || currency != "INR")
            throw new BusinessException(LibraryFinanceErrorCodes.SourceRequiresReview,
                "The assessment requires explicit year, school-date and currency evidence.");
        AcademicYearId = academicYearId;
        AcademicYearProvenance = yearProvenance;
        AcademicYearEvidenceJson = yearEvidenceJson;
        SchoolChargeDate = chargeDate;
        SchoolDueDate = dueDate;
        ChargeDateProvenance = chargeProvenance;
        DueDateProvenance = dueProvenance;
        SourceDateEvidenceJson = dateEvidenceJson;
        Currency = currency;
    }

    public void BindFinanceSource(Guid linkId, string acceptedAssessmentVersion)
    {
        EnsurePending();
        if (linkId == Guid.Empty || string.IsNullOrWhiteSpace(acceptedAssessmentVersion) || acceptedAssessmentVersion.Length > 64 ||
            FinanceSourceLinkId.HasValue && (FinanceSourceLinkId != linkId || GetAssessmentVersion() != acceptedAssessmentVersion))
            throw new BusinessException(LibraryFinanceErrorCodes.SourceAlreadyLinked,
                "The assessment is already bound to a different Finance source.");
        if (!FinanceSourceLinkId.HasValue)
            this.SetProperty(BoundAssessmentVersionProperty, acceptedAssessmentVersion);
        FinanceSourceLinkId = linkId;
    }

    public string GetAssessmentVersion() => FinanceSourceLinkId.HasValue
        ? this.GetProperty<string>(BoundAssessmentVersionProperty) ?? ConcurrencyStamp : ConcurrencyStamp;

    public void EnsureCanDelete() => EnsureCanAmend();

    public void EnsureCanAmend(ChargePaymentStatus? paymentStatus = null, DateTime? paidDate = null)
    {
        EnsurePending();
        if (FinanceSourceLinkId.HasValue)
            throw new BusinessException(LibraryFinanceErrorCodes.SourceAlreadyLinked,
                "This assessment is bound to Finance. Use its financial correction workflow.");
        if ((paymentStatus.HasValue && paymentStatus != ChargePaymentStatus.Pending) || paidDate.HasValue)
            throw new BusinessException(LibraryFinanceErrorCodes.SettlementRequiresFinance,
                "Record library charge payments and waivers through Finance.");
    }

    private void EnsurePending()
    {
        if (PaymentStatus != ChargePaymentStatus.Pending)
            throw Invalid("Paid or waived library charges cannot be edited or deleted. Use a linked financial correction.");
    }

    private static void ValidateAmount(decimal amount)
    {
        if (amount <= 0 || amount > 9999999999999999.99m || decimal.Round(amount, 2) != amount)
            throw Invalid("Charge amount must be positive and have at most two decimal places.");
    }

    private static void ValidateDates(DateTime chargeDate, DateTime dueDate)
    {
        if (chargeDate == default || dueDate == default || dueDate.Date < chargeDate.Date)
            throw Invalid("Charge date and due date are required, and due date cannot be before charge date.");
    }

    private static void ValidateSchoolDates(DateOnly chargeDate, DateOnly dueDate)
    {
        if (chargeDate == default || dueDate == default || dueDate < chargeDate)
            throw Invalid("School assessment and due dates are required, and due date cannot precede assessment date.");
    }

    private static BusinessException Invalid(string message) => new(LibraryErrorCodes.InvalidCharge, message);
}

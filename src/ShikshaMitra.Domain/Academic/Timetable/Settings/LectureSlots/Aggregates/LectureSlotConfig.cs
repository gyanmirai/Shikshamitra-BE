using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Academic.Timetable.Settings.LectureSlots;

public class LectureSlotConfig : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    #region Properties
    public Guid? TenantId { get; private set; }
    public Guid StandardId { get; private set; }
    public string SchoolHours { get; private set; }
    public Guid? PreviousStandardId { get; private set; }
    public virtual ICollection<LectureSlotGroup> SlotGroups { get; private set; }
    #endregion

    #region Constructors
    private LectureSlotConfig()
    {
        /* This constructor is for deserialization / ORM purpose */
        SlotGroups = new List<LectureSlotGroup>();
    }

    public LectureSlotConfig(
        Guid id,
        Guid? tenantId,
        Guid standardId,
        string schoolHours,
        Guid? previousStandardId = null)
        : base(id)
    {
        TenantId = tenantId;
        StandardId = standardId;
        SetSchoolHours(schoolHours);
        PreviousStandardId = previousStandardId;
        SlotGroups = new List<LectureSlotGroup>();
    }
    #endregion

    #region Methods
    public void SetSchoolHours(string schoolHours)
    {
        SchoolHours = Check.NotNullOrWhiteSpace(schoolHours, nameof(schoolHours),
            maxLength: TimetableConsts.MaxSchoolHoursLength);
    }

    public void SetPreviousStandardId(Guid? previousStandardId)
    {
        PreviousStandardId = previousStandardId;
    }

    public void AddSlotGroup(LectureSlotGroup group)
    {
        SlotGroups.Add(group);
    }

    public void ClearSlotGroups()
    {
        SlotGroups.Clear();
    }

    public void ReplaceSlotGroups(IEnumerable<LectureSlotGroup> groups)
    {
        SlotGroups.Clear();
        foreach (var group in groups)
        {
            SlotGroups.Add(group);
        }
    }
    #endregion
}

using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts;
using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Domain.Finance.Accounts.Settings;

public class TransactionCategory : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }
    public Guid AcademicYearId { get; set; }
    public TransactionType? CategoryType { get; private set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }

    private List<Tag> _tags = new List<Tag>();
    public IReadOnlyCollection<Tag> Tags => _tags.AsReadOnly();

    protected TransactionCategory() { }

    public TransactionCategory(
        Guid id,
        Guid? tenantId,
        Guid academicYearId,
        TransactionType categoryType,
        string name,
        string? description,
        IEnumerable<string>? tags = null,
        bool isActive = true)
        : base(id)
    {
        TenantId = tenantId;
        AcademicYearId = academicYearId;
        CategoryType = categoryType;
        Name = name;
        Description = description;
        IsActive = isActive;

        if (tags != null)
        {
            foreach (var tag in tags)
                AddTag(tag);
        }
    }

    public void AddTag(string value)
    {
        var tag = new Tag(value);
        if (_tags.All(t => t.Value != tag.Value))
            _tags.Add(tag);
    }

    public void RemoveTag(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        _tags.RemoveAll(t => t.Value == normalized);
    }

    public void ClearTags() => _tags.Clear();

    public void SetTags(IEnumerable<string> tags)
    {
        _tags.Clear();
        foreach (var tag in tags)
            AddTag(tag);
    }
}

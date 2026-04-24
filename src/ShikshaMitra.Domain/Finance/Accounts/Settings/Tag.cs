using System;
using System.Collections.Generic;
using Volo.Abp;
using Volo.Abp.Domain.Values;

namespace ShikshaMitra.Finance.Accounts.Settings;

public class Tag : ValueObject
{
    public const int MaxLength = 64;

    public string Value { get; private set; }

    protected Tag() { }

    public Tag(string value)
    {
        Check.NotNullOrWhiteSpace(value, nameof(value));
        Check.Length(value, nameof(value), MaxLength);
        Value = value.Trim().ToLowerInvariant();
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return Value;
    }
}

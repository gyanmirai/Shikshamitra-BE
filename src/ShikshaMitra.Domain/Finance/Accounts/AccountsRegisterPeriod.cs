using System;
using Volo.Abp;

namespace ShikshaMitra.Finance.Accounts;

/// <summary>India calendar month clipped to the selected academic date interval.</summary>
public static class AccountsRegisterPeriod
{
    public static (DateTime StartUtc, DateTime EndUtc) Resolve(DateTime start, DateTime end, int? month, int? year)
    {
        if (start.Date > end.Date || month.HasValue != year.HasValue ||
            month is < 1 or > 12 || year is < 1 or > 9998)
            throw new BusinessException(AccountsRegisterErrorCodes.InvalidPeriod);
        var from = start.Date;
        var until = end.Date.AddDays(1);
        if (month.HasValue)
        {
            var first = new DateTime(year!.Value, month.Value, 1);
            var next = first.AddMonths(1);
            if (first >= until || next <= from)
                throw new BusinessException(AccountsRegisterErrorCodes.InvalidPeriod);
            from = first > from ? first : from;
            until = next < until ? next : until;
        }
        return (DateTime.SpecifyKind(from.AddMinutes(-330), DateTimeKind.Utc),
            DateTime.SpecifyKind(until.AddMinutes(-330), DateTimeKind.Utc));
    }
}

using System.Collections.Generic;

namespace ShikshaMitra.Groups;

public class UserFilter
{
    public string Field { get; set; }
    public UserFilterOperator Operator { get; set; }
    public string Value { get; set; }
}

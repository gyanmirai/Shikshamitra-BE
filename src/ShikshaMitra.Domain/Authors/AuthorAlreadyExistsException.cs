using Volo.Abp;

namespace ShikshaMitra.Authors;

public class AuthorAlreadyExistsException : BusinessException
{
    public AuthorAlreadyExistsException(string name)
        : base(ShikshaMitraDomainErrorCodes.AuthorAlreadyExists)
    {
        WithData("name", name);
    }
}
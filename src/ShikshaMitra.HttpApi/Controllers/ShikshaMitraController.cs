using ShikshaMitra.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers;

/* Inherit your controllers from this class.
 */
public abstract class ShikshaMitraController : AbpControllerBase
{
    protected ShikshaMitraController()
    {
        LocalizationResource = typeof(ShikshaMitraResource);
    }
}

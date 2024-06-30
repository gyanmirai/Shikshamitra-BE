using Volo.Abp.DependencyInjection;
using Volo.Abp.Ui.Branding;

namespace ShikshaMitra;

[Dependency(ReplaceServices = true)]
public class ShikshaMitraBrandingProvider : DefaultBrandingProvider
{
    public override string AppName => "ShikshaMitra";
}

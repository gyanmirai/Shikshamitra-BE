using Volo.Abp.Features;

namespace ShikshaMitra;

public class ShikshaMitraFeatureDefinitionProvider : FeatureDefinitionProvider
{
    public override void Define(IFeatureDefinitionContext context)
    {
        FeatureGroupDefinition shikshaMitraGroup = context.AddGroup("ShikshaMitra");

        shikshaMitraGroup.AddFeature("ShikshaMitra.PdfReporting", defaultValue: "false");
        shikshaMitraGroup.CreateChildFeature("ShikshaMitra.PdfReporting.StudentReport", defaultValue: "false");

    }
}

using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using EcommerceAIAgent.Shared.Resources;
using Spiderly.Shared.Extensions;
using Spiderly.Shared.Resources;

namespace EcommerceAIAgent.Shared.FluentValidation
{
    public class TranslatePropertiesConfiguration : IConfigureOptions<MvcOptions>
    {
        public TranslatePropertiesConfiguration()
        {

        }

        public void Configure(MvcOptions options)
        {
            ValidatorOptions.Global.DisplayNameResolver = (type, memberInfo, expression) =>
            {
                string translatedPropertyName =
                    TermsGenerated.ResourceManager.GetTranslation(memberInfo.Name) ??
                    Terms.ResourceManager.GetTranslation(memberInfo.Name) ??
                    SharedTerms.ResourceManager.GetTranslation(memberInfo.Name);

                return translatedPropertyName;
            };
        }
    }
}


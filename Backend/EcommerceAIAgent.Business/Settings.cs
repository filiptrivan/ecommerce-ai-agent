using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EcommerceAIAgent.Business
{
    public static class SettingsProvider
    {
        public static Settings Current { internal get; set; } = new Settings();
    }

    public class Settings
    {
        public string OpenAIApiKey { get; set; }
        public string ExternalApiBearerToken { get; set; }
        public string QdrantApiKey { get; set; }
        public string QdrantProductsTableName { get; set; }
    }
}

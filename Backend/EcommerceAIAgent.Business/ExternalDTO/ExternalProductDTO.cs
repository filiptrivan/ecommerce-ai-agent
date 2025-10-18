using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EcommerceAIAgent.Business.ExternalDTO
{
    public class ExternalProductDTO
    {
        public int id { get; set; }
        public string title { get; set; }
        public string url { get; set; }
        public int price { get; set; }
        public int? sale_price { get; set; }
        public int stock { get; set; }
    }
}

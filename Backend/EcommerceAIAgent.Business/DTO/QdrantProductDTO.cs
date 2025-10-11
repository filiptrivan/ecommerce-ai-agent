using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EcommerceAIAgent.Business.DTO
{
    public class QdrantProductDTO
    {
        [Name("id")]
        public string Id { get; set; }

        [Name("text")]
        public string Text { get; set; }

        /// <summary>
        /// General guidance price, we are saying to LLM that it needs to check via API for the exact price
        /// </summary>
        [Name("price")]
        public string Price { get; set; }
    }


}

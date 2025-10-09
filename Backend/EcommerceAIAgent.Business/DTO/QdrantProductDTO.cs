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

        [Name("title")]
        public string Title { get; set; }

        [Name("description")]
        public string Description { get; set; }

        [Name("text")]
        public string Text { get; set; }

        [Name("category")]
        public string Category { get; set; }

        public string ToSearchableText()
        {
            return $"{Title}. {Description}. {Text}. Category: {Category}";
        }
    }
}

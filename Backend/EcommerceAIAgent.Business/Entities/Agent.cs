using Microsoft.EntityFrameworkCore;
using Spiderly.Shared.BaseEntities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EcommerceAIAgent.Business.Entities
{
    public class Agent : BusinessObject<long>
    {
        [Required]
        [Precision(3, 2)]
        [Range(0, 1)]
        public decimal Temperature { get; set; }
    }
}

using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EcommerceAIAgent.Business.DTO
{
    public class MessageDTO
    {
        public string Content { get; set; }
        public string Role { get; set; }
        public List<MessageDTO> ChatHistory { get; set; }
    }
}

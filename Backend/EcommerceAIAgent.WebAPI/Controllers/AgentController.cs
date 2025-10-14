using Microsoft.AspNetCore.Mvc;
using Azure.Storage.Blobs;
using Spiderly.Shared.Attributes;
using Spiderly.Shared.Attributes.Entity.UI;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.DTO;
using EcommerceAIAgent.Business.DTO;
using EcommerceAIAgent.Business.Services;

namespace EcommerceAIAgent.WebAPI.Controllers
{
    [ApiController]
    [Route("/api/[controller]/[action]")]
    public class AgentController : AgentBaseController
    {
        private readonly IApplicationDbContext _context;
        private readonly EcommerceAIAgentBusinessService _ecommerceAIAgentBusinessService;

        public AgentController(
            IApplicationDbContext context,
            EcommerceAIAgentBusinessService ecommerceAIAgentBusinessService
        )
            : base(context, ecommerceAIAgentBusinessService)
        {
            _context = context;
            _ecommerceAIAgentBusinessService = ecommerceAIAgentBusinessService;
        }

        [HttpGet]
        [AuthGuard]
        public async Task SaveProductsToVectorDb()
        {
            await _ecommerceAIAgentBusinessService.SaveProductsToVectorDb();
        }

        [HttpGet]
        [AuthGuard]
        public async Task<string> SendMessage(string userPrompt)
        {
            return await _ecommerceAIAgentBusinessService.SendMessage(null, userPrompt);
        }
    }
}

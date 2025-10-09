using Microsoft.AspNetCore.Mvc;
using Spiderly.Shared.Attributes;
using Spiderly.Shared.Interfaces;
using Azure.Storage.Blobs;
using Spiderly.Shared.DTO;
using Spiderly.Shared.Resources;
using Spiderly.Security.Services;
using EcommerceAIAgent.Business.Services;
using EcommerceAIAgent.Business.DTO;
using EcommerceAIAgent.Business.Entities;

namespace EcommerceAIAgent.WebAPI.Controllers
{
    [ApiController]
    [Route("/api/[controller]/[action]")]
    public class UserController : UserBaseController
    {
        private readonly IApplicationDbContext _context;
        private readonly EcommerceAIAgentBusinessService _ecommerceAIAgentBusinessService;
        private readonly AuthenticationService _authenticationService;

        public UserController(
            IApplicationDbContext context, 
            EcommerceAIAgentBusinessService ecommerceAIAgentBusinessService, 
            AuthenticationService authenticationService
        )
            : base(context, ecommerceAIAgentBusinessService)
        {
            _context = context;
            _ecommerceAIAgentBusinessService = ecommerceAIAgentBusinessService;
            _authenticationService = authenticationService;
        }

        [HttpGet]
        [AuthGuard]
        [SkipSpinner]
        public async Task<UserDTO> GetCurrentUser()
        {
            long userId = _authenticationService.GetCurrentUserId();
            return await _ecommerceAIAgentBusinessService.GetUserDTO(userId, false); // Don't need to authorize because he is current user
        }

    }
}


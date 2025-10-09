using Microsoft.AspNetCore.Mvc;
using Spiderly.Security.Interfaces;
using Spiderly.Security.Services;
using Spiderly.Security.SecurityControllers;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Attributes;
using Spiderly.Shared.DTO;
using Microsoft.EntityFrameworkCore;
using Spiderly.Shared.Resources;
using Spiderly.Security.DTO;
using Spiderly.Shared.Extensions;
using EcommerceAIAgent.Business.Entities;
using EcommerceAIAgent.Business.Services;
using EcommerceAIAgent.Business.DTO;

namespace EcommerceAIAgent.WebAPI.Controllers
{
    [ApiController]
    [Route("/api/[controller]/[action]")]
    public class SecurityController : SecurityBaseController<User>
    {
        private readonly ILogger<SecurityController> _logger;
        private readonly SecurityBusinessService<User> _securityBusinessService;
        private readonly IApplicationDbContext _context;
        private readonly EcommerceAIAgentBusinessService _ecommerceAIAgentBusinessService;


        public SecurityController(
            ILogger<SecurityController> logger, 
            SecurityBusinessService<User> securityBusinessService, 
            IJwtAuthManager jwtAuthManagerService, 
            IApplicationDbContext context, 
            AuthenticationService authenticationService,
            AuthorizationService authorizationService,
            EcommerceAIAgentBusinessService ecommerceAIAgentBusinessService
        )
            : base(securityBusinessService, jwtAuthManagerService, context, authenticationService, authorizationService)
        {
            _logger = logger;
            _securityBusinessService = securityBusinessService;
            _context = context;
            _ecommerceAIAgentBusinessService = ecommerceAIAgentBusinessService;
        }

       

    }
}


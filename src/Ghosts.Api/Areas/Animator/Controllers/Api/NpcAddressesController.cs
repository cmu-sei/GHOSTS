using System.Net;
using Ghosts.Animator;
using Ghosts.Animator.Models;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace ghosts.api.Areas.Animator.Controllers.Api
{
    [ApiController]
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class NpcAddressesController: ControllerBase
    {
        /// <summary>
        /// Get random addressProfile
        /// </summary>
        /// <returns></returns>
        [ProducesResponseType(typeof(AddressProfiles.AddressProfile), (int) HttpStatusCode.OK)]
        [SwaggerResponse((int) HttpStatusCode.OK, Type = typeof(AddressProfiles.AddressProfile))]
        [SwaggerOperation("getAddress")]
        [HttpGet]
        public AddressProfiles.AddressProfile Get()
        {
            return Address.GetHomeAddress();
        }
        
        /// <summary>
        /// Get random city
        /// </summary>
        /// <returns></returns>
        [ProducesResponseType(typeof(string), (int) HttpStatusCode.OK)]
        [SwaggerResponse((int) HttpStatusCode.OK, Type = typeof(string))]
        [SwaggerOperation("getCity")]
        [HttpGet("cities")]
        public string GetCity()
        {
            return Address.GetCity();
        }

        /// <summary>
        /// Get random US state
        /// </summary>
        /// <returns></returns>
        [ProducesResponseType(typeof(string), (int) HttpStatusCode.OK)]
        [SwaggerResponse((int) HttpStatusCode.OK, Type = typeof(string))]
        [SwaggerOperation("getState")]
        [HttpGet("states")]
        public string GetState()
        {
            return Address.GetUSStateName();
        }
        
        /// <summary>
        /// Get random country
        /// </summary>
        /// <returns></returns>
        [ProducesResponseType(typeof(string), (int) HttpStatusCode.OK)]
        [SwaggerResponse((int) HttpStatusCode.OK, Type = typeof(string))]
        [SwaggerOperation("getCountry")]
        [HttpGet("countries")]
        public string GetCountry()
        {
            return Address.GetCountry();
        }
    }
}
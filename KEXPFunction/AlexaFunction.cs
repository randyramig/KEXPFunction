using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace KEXPFunction
{
    using Alexa.NET;
    using Alexa.NET.Request;
    using Alexa.NET.Request.Type;
    using Alexa.NET.Response;

    public static class AlexaFunction
    {
        [FunctionName("Alexa")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            string json = await req.ReadAsStringAsync();
            var skillRequest = JsonConvert.DeserializeObject<SkillRequest>(json);

            var requestType = skillRequest.GetRequestType();

            SkillResponse response = null;
            if (requestType == typeof(LaunchRequest))
            {
                response = ResponseBuilder.Tell("Welcome to KEXP 90.3FM");
                response.Response.ShouldEndSession = false;
            }

            return new OkObjectResult(response);
        }
    }
}

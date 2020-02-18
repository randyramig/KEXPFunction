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

            bool isValid = await ValidateRequest(req, log, skillRequest);
            if (!isValid)
            {
                return new BadRequestResult();
            }

            var requestType = skillRequest.GetRequestType();

            log.LogInformation("Request received, type: {0} id: {1}", requestType, skillRequest.Request.RequestId);

            SkillResponse response = null;
            if (requestType == typeof(LaunchRequest))
            {
                response = ResponseBuilder.Tell("Welcome to KEXP 90.3FM");
                response.Response.ShouldEndSession = false;
            }
            else if (requestType == typeof(IntentRequest))
            {
                var intentRequest = skillRequest.Request as IntentRequest;

                log.LogInformation("-> IntentRequest, name: {0}", intentRequest.Intent.Name);

                if (intentRequest.Intent.Name == "Play" || intentRequest.Intent.Name == "AMAZON.ResumeIntent" || intentRequest.Intent.Name == "AMAZON.StartOverIntent")
                {
                    //handle the intent
                    // https://live-aacplus-64.streamguys1.com/ -- NOTE should we go with higher bitrate?
                    response = ResponseBuilder.AudioPlayerPlay(Alexa.NET.Response.Directive.PlayBehavior.ReplaceAll, "https://live-aacplus-64.streamguys1.com/", "token");
                }
                else if (intentRequest.Intent.Name == "AMAZON.CancelIntent")
                {
                    response = ResponseBuilder.AudioPlayerStop();
                }
                else if (intentRequest.Intent.Name == "AMAZON.HelpIntent")
                {
                    response = ResponseBuilder.Tell("Say PLAY to play the KEXP live stream");

                    response.Response.ShouldEndSession = false;
                }
                else if (intentRequest.Intent.Name == "AMAZON.PauseIntent")
                {
                    response = ResponseBuilder.AudioPlayerStop();
                }
                else if (intentRequest.Intent.Name == "AMAZON.StopIntent")
                {
                    response = ResponseBuilder.AudioPlayerStop();
                }
                else if (intentRequest.Intent.Name == "AMAZON.NextIntent" || intentRequest.Intent.Name == "AMAZON.PreviousIntent")
                {
                    response = ResponseBuilder.Tell("Sorry, Next and Previous are not supported.");
                }
                else if (intentRequest.Intent.Name == "AMAZON.LoopOnIntent" || intentRequest.Intent.Name == "AMAZON.LoopOffIntent")
                {
                    response = ResponseBuilder.Tell("Sorry, looping is not supported.");
                }
                else if (intentRequest.Intent.Name == "AMAZON.ShuffleOnIntent" || intentRequest.Intent.Name == "AMAZON.ShuffleOffIntent")
                {
                    response = ResponseBuilder.Tell("Sorry, shuffle is not supported.");
                }
                else if (intentRequest.Intent.Name == "AMAZON.RepeatIntent")
                {
                    response = ResponseBuilder.Tell("Sorry, Repeat is not supported.");
                }
            }
            else if (requestType == typeof(AudioPlayerRequest))
            {
                // Don't do anything with these for now, but handle them gracefully.
                // More info: https://developer.amazon.com/en-US/docs/alexa/custom-skills/audioplayer-interface-reference.html
                var audioPlayerRequest = skillRequest.Request as AudioPlayerRequest;
                log.LogInformation("-> AudioPlayerRequest: {0}", audioPlayerRequest.AudioRequestType);
                response = ResponseBuilder.Empty();
            }
            else if (requestType == typeof(SessionEndedRequest))
            {
                log.LogInformation("-> Session ended");
                response = ResponseBuilder.Empty();
                response.Response.ShouldEndSession = true;
            }

            if (response == null)
            {
                log.LogError("*** Unhandled request:");
                log.LogError(json);
            }

            return new OkObjectResult(response);
        }

        private static async Task<bool> ValidateRequest(HttpRequest request, ILogger log, SkillRequest skillRequest)
        {
            request.Headers.TryGetValue("SignatureCertChainUrl", out var signatureChainUrl);
            if (string.IsNullOrWhiteSpace(signatureChainUrl))
            {
                log.LogWarning("Validation failed. Empty SignatureCertChainUrl header");
                return false;
            }

            Uri certUrl;
            try
            {
                certUrl = new Uri(signatureChainUrl);
            }
            catch
            {
                log.LogWarning($"Validation failed. SignatureChainUrl not valid: {signatureChainUrl}");
                return false;
            }

            request.Headers.TryGetValue("Signature", out var signature);
            if (string.IsNullOrWhiteSpace(signature))
            {
                log.LogWarning("Validation failed - Empty Signature header");
                return false;
            }

            request.Body.Position = 0;
            var body = await request.ReadAsStringAsync();
            request.Body.Position = 0;

            if (string.IsNullOrWhiteSpace(body))
            {
                log.LogWarning("Validation failed - the JSON is empty");
                return false;
            }

            bool isTimestampValid = RequestVerification.RequestTimestampWithinTolerance(skillRequest);
            bool valid = await RequestVerification.Verify(signature, certUrl, body);

            if (!valid || !isTimestampValid)
            {
                log.LogWarning("Validation failed - RequestVerification failed");
                return false;
            }
            else
            {
                return true;
            }
        }
    }
}

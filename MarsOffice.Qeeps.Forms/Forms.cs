using System;
using System.IO;
using System.Threading.Tasks;
using FluentValidation;
using MarsOffice.Qeeps.Forms.Abstractions;
using MarsOffice.Qeeps.Microfunction;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MarsOffice.Qeeps.Forms
{
    public class Forms
    {
        private readonly IValidator<FormDto> _validator;

        public Forms(IValidator<FormDto> validator)
        {
            _validator = validator;
        }

        [FunctionName("CreateForm")]
        public async Task<IActionResult> CreateForm(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/forms/create")] HttpRequest req,
            ILogger log
        )
        {
            try
            {
                var json = string.Empty;
                using (var streamReader = new StreamReader(req.Body))
                {
                    json = await streamReader.ReadToEndAsync();
                }
                var payload = JsonConvert.DeserializeObject<FormDto>(json, new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                });
                await _validator.ValidateAndThrowAsync(payload);
                return null;
            }
            catch (Exception e)
            {
                log.LogError(e, "Exception occured in function");
                return new BadRequestObjectResult(Errors.Extract(e));
            }
        }

        [FunctionName("GetForms")]
        public async Task<IActionResult> GetMyForms(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/forms/getMyForms")] HttpRequest req,
            ILogger log
        )
        {
            try
            {
                // TODO
                return null;
            }
            catch (Exception e)
            {
                log.LogError(e, "Exception occured in function");
                return new BadRequestObjectResult(Errors.Extract(e));
            }
        }
    }
}

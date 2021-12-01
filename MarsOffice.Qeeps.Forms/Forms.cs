using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using FluentValidation;
using MarsOffice.Qeeps.Access.Abstractions;
using MarsOffice.Qeeps.Forms.Abstractions;
using MarsOffice.Qeeps.Microfunction;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MarsOffice.Qeeps.Forms
{
    public class Forms
    {
        private readonly IValidator<FormDto> _formDtoValidator;
        private readonly IHttpClientFactory _httpClientFactory;

        public Forms(IValidator<FormDto> formDtoValidator, IHttpClientFactory httpClientFactory)
        {
            _formDtoValidator = formDtoValidator;
            _httpClientFactory = httpClientFactory;
        }

        [FunctionName("CreateForm")]
        public async Task<IActionResult> CreateForm(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/forms/create")] HttpRequest req,
            [CosmosDB(ConnectionStringSetting = "cdbconnectionstring", PreferredLocations = "%location%")] DocumentClient client,
            ILogger log
        )
        {
            try
            {
#if DEBUG
                var db = new Database
                {
                    Id = "forms"
                };
                await client.CreateDatabaseIfNotExistsAsync(db);

                var col = new DocumentCollection
                {
                    Id = "Forms",
                    PartitionKey = new PartitionKeyDefinition
                    {
                        Version = PartitionKeyDefinitionVersion.V2,
                        Paths = new System.Collections.ObjectModel.Collection<string>(new List<string>() { "/UserId" })
                    }
                };
                await client.CreateDocumentCollectionIfNotExistsAsync(UriFactory.CreateDatabaseUri("forms"), col);

                col = new DocumentCollection
                {
                    Id = "FormAccesses",
                    PartitionKey = new PartitionKeyDefinition
                    {
                        Version = PartitionKeyDefinitionVersion.V2,
                        Paths = new System.Collections.ObjectModel.Collection<string>(new List<string>() { "/FormId" })
                    }
                };
                await client.CreateDocumentCollectionIfNotExistsAsync(UriFactory.CreateDatabaseUri("forms"), col);
#endif

                var principal = QeepsPrincipal.Parse(req);
                var uid = principal.FindFirst("id").Value;

                // validate role
                if (!principal.FindAll("roles").Any(x => x.Value == "Admin" || x.Value == "Owner"))
                {
                    return new StatusCodeResult(401);
                }

                var json = string.Empty;
                using (var streamReader = new StreamReader(req.Body))
                {
                    json = await streamReader.ReadToEndAsync();
                }
                var payload = JsonConvert.DeserializeObject<FormDto>(json, new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                });
                await _formDtoValidator.ValidateAndThrowAsync(payload);

                // validate orgs
                if (payload.FormAccesses?.Any() == true)
                {
                    using var accessClient = _httpClientFactory.CreateClient("access");
                    var orgsResponse = await accessClient.GetStringAsync("/api/access/getFullOrganisationsTree/" + uid);
                    var userOrgs = JsonConvert.DeserializeObject<IEnumerable<OrganisationDto>>(orgsResponse, new JsonSerializerSettings
                    {
                        ContractResolver = new CamelCasePropertyNamesContractResolver()
                    });
                    if (payload.FormAccesses.Any(fa => !userOrgs.Any(uo => uo.Id == fa.OrganisationId)))
                    {
                        return new StatusCodeResult(401);
                    }
                }
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

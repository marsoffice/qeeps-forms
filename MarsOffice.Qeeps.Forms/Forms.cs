using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AutoMapper;
using FluentValidation;
using MarsOffice.Qeeps.Access.Abstractions;
using MarsOffice.Qeeps.Forms.Abstractions;
using MarsOffice.Qeeps.Forms.Entities;
using MarsOffice.Qeeps.Microfunction;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
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
        private readonly IMapper _mapper;

        public Forms(IValidator<FormDto> formDtoValidator, IHttpClientFactory httpClientFactory, IMapper mapper)
        {
            _formDtoValidator = formDtoValidator;
            _httpClientFactory = httpClientFactory;
            _mapper = mapper;
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

                // map and save form
                var entity = _mapper.Map<FormEntity>(payload);
                entity.UserId = uid;
                entity.CreatedDate = DateTime.UtcNow;

                var formsCollection = UriFactory.CreateDocumentCollectionUri("forms", "Forms");

                var insertFormResponse = await client.UpsertDocumentAsync(formsCollection, entity, new RequestOptions
                {
                    PartitionKey = new PartitionKey(uid)
                });
                entity.Id = insertFormResponse.Resource.Id;
                var dto = _mapper.Map<FormDto>(entity);
                return new OkObjectResult(dto);
            }
            catch (Exception e)
            {
                log.LogError(e, "Exception occured in function");
                return new BadRequestObjectResult(Errors.Extract(e));
            }
        }

        [FunctionName("GetForms")]
        public async Task<IActionResult> GetForms(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/forms/getForms")] HttpRequest req,
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
#endif

                var principal = QeepsPrincipal.Parse(req);
                var uid = principal.FindFirst("id").Value;

                int? page = null;
                int? elementsPerPage = null;

                if (req.Query.ContainsKey("page"))
                {
                    page = int.Parse(req.Query["page"].ToString());
                }

                if (req.Query.ContainsKey("elementsPerPage"))
                {
                    elementsPerPage = int.Parse(req.Query["elementsPerPage"].ToString());
                }

                DateTime? startDate = null;
                DateTime? endDate = null;

                if (req.Query.ContainsKey("startDate"))
                {
                    startDate = DateTime.Parse(req.Query["startDate"].ToString()).ToUniversalTime();
                }

                if (req.Query.ContainsKey("endDate"))
                {
                    endDate = DateTime.Parse(req.Query["endDate"].ToString()).ToUniversalTime();
                }

                using var accessClient = _httpClientFactory.CreateClient("access");
                var orgsResponse = await accessClient.GetStringAsync("/api/access/getAccessibleOrganisations/" + uid);
                var userOrgs = JsonConvert.DeserializeObject<IEnumerable<OrganisationDto>>(orgsResponse, new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                });
                var userOrgIds = userOrgs.Select(x => x.FullId)
                    .SelectMany(x => x.Split("_").Skip(1).ToList())
                    .Distinct().ToList();

                var formsCollection = UriFactory.CreateDocumentCollectionUri("forms", "Forms");

                var queryable = client.CreateDocumentQuery<FormEntity>(formsCollection, new FeedOptions
                {
                    EnableCrossPartitionQuery = true
                }).Where(x =>
                x.UserId == uid ||
                (x.FormAccesses != null && x.FormAccesses.Any(fa => userOrgIds.Contains(fa.OrganisationId))));

                // TODO search and filter, order

                if (startDate != null)
                {
                    queryable = queryable.Where(x => x.CreatedDate >= startDate);
                }

                if (endDate != null)
                {
                    queryable = queryable.Where(x => x.CreatedDate < endDate);
                }

                queryable = queryable.OrderByDescending(x => x.CreatedDate);

                if (page != null && elementsPerPage != null)
                {
                    queryable = queryable.Skip(page.Value * elementsPerPage.Value)
                    .Take(elementsPerPage.Value);
                }

                var query = queryable.AsDocumentQuery();

                var formDtos = new List<FormDto>();

                while (query.HasMoreResults)
                {
                    var response = await query.ExecuteNextAsync<FormEntity>();
                    formDtos.AddRange(_mapper.Map<IEnumerable<FormDto>>(response));
                }

                return new OkObjectResult(formDtos);
            }
            catch (Exception e)
            {
                log.LogError(e, "Exception occured in function");
                return new BadRequestObjectResult(Errors.Extract(e));
            }
        }

        [FunctionName("GetForm")]
        public async Task<IActionResult> GetForm(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/forms/getForm/{id}")] HttpRequest req,
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
#endif

                var principal = QeepsPrincipal.Parse(req);
                var uid = principal.FindFirst("id").Value;
                var formId = req.RouteValues["id"].ToString();
                var formsCollectionUri = UriFactory.CreateDocumentCollectionUri("forms", "Forms");
                var query = client.CreateDocumentQuery<FormEntity>(formsCollectionUri, new FeedOptions
                {
                    EnableCrossPartitionQuery = true
                })
                .Where(x => x.Id == formId)
                .Take(1)
                .AsDocumentQuery();

                if (!query.HasMoreResults)
                {
                    return new NotFoundResult();
                }

                var response = await query.ExecuteNextAsync<FormEntity>();

                if (response.Count == 0)
                {
                    return new NotFoundResult();
                }

                var entity = response.First();

                if (entity.UserId == uid)
                {
                    return new OkObjectResult(_mapper.Map<FormDto>(entity));
                }
                using var accessClient = _httpClientFactory.CreateClient("access");
                var orgsResponse = await accessClient.GetStringAsync("/api/access/getAccessibleOrganisations/" + uid);
                var userOrgs = JsonConvert.DeserializeObject<IEnumerable<OrganisationDto>>(orgsResponse, new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                });
                var userOrgIds = userOrgs.Select(x => x.FullId)
                    .SelectMany(x => x.Split("_").Skip(1).ToList())
                    .Distinct().ToList();
                if (entity.FormAccesses == null || !entity.FormAccesses.Any(fa => userOrgIds.Contains(fa.OrganisationId)))
                {
                    return new StatusCodeResult(401);
                }
                return new OkObjectResult(_mapper.Map<FormDto>(entity));
            }
            catch (Exception e)
            {
                log.LogError(e, "Exception occured in function");
                return new BadRequestObjectResult(Errors.Extract(e));
            }
        }
    }
}

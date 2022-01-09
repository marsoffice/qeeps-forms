using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Threading.Tasks;
using AutoMapper;
using FluentValidation;
using MarsOffice.Microfunction;
using MarsOffice.Qeeps.Access.Abstractions;
using MarsOffice.Qeeps.Forms.Abstractions;
using MarsOffice.Qeeps.Forms.Entities;
using MarsOffice.Qeeps.Notifications.Abstractions;
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
            ILogger log,
            [ServiceBus(
                #if DEBUG
                "notifications-dev",
                #else
                "notifications",
                #endif
                 Connection = "sbconnectionstring")] IAsyncCollector<RequestNotificationDto> outputNotifications
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

                var principal = MarsOfficePrincipal.Parse(req);
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
                    var orgsResponse = await accessClient.GetStringAsync("/api/access/getAccessibleOrganisations/" + uid);
                    var userOrgs = JsonConvert.DeserializeObject<IEnumerable<GroupDto>>(orgsResponse, new JsonSerializerSettings
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
                if (entity.Tags != null)
                {
                    entity.Tags = entity.Tags.Select(x => x.ToLower()).ToList();
                }
                entity.UserId = uid;
                entity.UserName = principal.FindFirst("name").Value.ToString();
                entity.CreatedDate = DateTime.UtcNow;

                var formsCollection = UriFactory.CreateDocumentCollectionUri("forms", "Forms");

                var insertFormResponse = await client.UpsertDocumentAsync(formsCollection, entity, new RequestOptions
                {
                    PartitionKey = new PartitionKey(uid)
                });
                entity.Id = insertFormResponse.Resource.Id;
                var dto = _mapper.Map<FormDto>(entity);

                // Notifications
                await SendNotificationsForFormEntity(uid, entity, outputNotifications, log, "FormCreated", payload.SendEmailNotifications);

                return new OkObjectResult(dto);
            }
            catch (Exception e)
            {
                log.LogError(e, "Exception occured in function");
                return new BadRequestObjectResult(Errors.Extract(e));
            }
        }

        [FunctionName("UpdateForm")]
        public async Task<IActionResult> UpdateForm(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "api/forms/update/{id}")] HttpRequest req,
            [CosmosDB(ConnectionStringSetting = "cdbconnectionstring", PreferredLocations = "%location%")] DocumentClient client,
            ILogger log,
            [ServiceBus(
                #if DEBUG
                "notifications-dev",
                #else
                "notifications",
                #endif
                 Connection = "sbconnectionstring")] IAsyncCollector<RequestNotificationDto> outputNotifications
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

                var principal = MarsOfficePrincipal.Parse(req);
                var uid = principal.FindFirst("id").Value;


                // validate role
                if (!principal.FindAll("roles").Any(x => x.Value == "Admin" || x.Value == "Owner"))
                {
                    return new StatusCodeResult(401);
                }

                var formId = req.RouteValues["id"].ToString();

                var documentUri = UriFactory.CreateDocumentUri("forms", "Forms", formId);
                var entityResponse = await client.ReadDocumentAsync<FormEntity>(documentUri, new RequestOptions
                {
                    PartitionKey = new PartitionKey(uid)
                });
                if (entityResponse.Document == null)
                {
                    return new NotFoundResult();
                }
                var entity = entityResponse.Document;
                if (entity.UserId != uid)
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
                _mapper.Map(payload, entity);
                entity.ModifiedDate = DateTime.UtcNow;
                if (entity.Tags != null)
                {
                    entity.Tags = entity.Tags.Select(x => x.ToLower()).ToList();
                }

                var formsCollectionUri = UriFactory.CreateDocumentCollectionUri("forms", "Forms");
                await client.UpsertDocumentAsync(formsCollectionUri, entity, new RequestOptions
                {
                    PartitionKey = new PartitionKey(uid)
                });

                var dto = _mapper.Map<FormDto>(entity);

                // Notifications
                await SendNotificationsForFormEntity(uid, entity, outputNotifications, log, "FormUpdated", payload.SendEmailNotifications);

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

                var principal = MarsOfficePrincipal.Parse(req);
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

                var search = req.Query.ContainsKey("search") ? req.Query["search"].ToString() : null;
                var sortBy = req.Query.ContainsKey("sortBy") ? req.Query["sortBy"].ToString() : null;
                var sortOrder = req.Query.ContainsKey("sortOrder") ? req.Query["sortOrder"].ToString() : null;
                var tags = req.Query.ContainsKey("tags") ? req.Query["tags"].ToString().Split(",").Select(x => x.ToLower()).ToList() : null;

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

                if (startDate != null)
                {
                    queryable = queryable.Where(x => x.CreatedDate >= startDate);
                }

                if (endDate != null)
                {
                    queryable = queryable.Where(x => x.CreatedDate < endDate);
                }

                if (!string.IsNullOrEmpty(search))
                {
                    var searchLower = search.ToLower();
                    queryable = queryable
                        .Where(x => x.Title.ToLower().Contains(searchLower) ||
                        x.UserName.ToLower().Contains(searchLower) ||
                        x.Description.ToLower().Contains(searchLower)
                        );
                }

                if (tags != null && tags.Any())
                {
                    queryable = queryable.Where(x => x.Tags != null && x.Tags.Any(t => tags.Contains(t)));
                }

                if (!string.IsNullOrEmpty(sortBy) && !string.IsNullOrEmpty(sortOrder))
                {
                    if (sortOrder == "asc")
                    {
                        queryable = queryable.OrderBy(GetPropertyExpression(sortBy));
                    }
                    else
                    {
                        queryable = queryable.OrderByDescending(GetPropertyExpression(sortBy));
                    }
                }

                var countQueryable = queryable;

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

                var total = await countQueryable
                    .CountAsync();


                return new OkObjectResult(new FormsListResultDto
                {
                    Forms = formDtos,
                    Total = total
                });
            }
            catch (Exception e)
            {
                log.LogError(e, "Exception occured in function");
                return new BadRequestObjectResult(Errors.Extract(e));
            }
        }

        [FunctionName("GetPinnedForms")]
        public async Task<IActionResult> GetPinnedForms(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/forms/getPinnedForms")] HttpRequest req,
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

                var principal = MarsOfficePrincipal.Parse(req);
                var uid = principal.FindFirst("id").Value;

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
                var today = DateTime.UtcNow;
                var queryable = client.CreateDocumentQuery<FormEntity>(formsCollection, new FeedOptions
                {
                    EnableCrossPartitionQuery = true
                }).Where(x =>
                (x.UserId == uid ||
                (x.FormAccesses != null && x.FormAccesses.Any(fa => userOrgIds.Contains(fa.OrganisationId)))
                ) && x.IsPinned && (x.PinnedUntilDate == null || x.PinnedUntilDate.Value > today)
                );

                queryable = queryable.OrderByDescending(x => x.CreatedDate);

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

                var principal = MarsOfficePrincipal.Parse(req);
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

        [FunctionName("DeleteForm")]
        public static async Task<IActionResult> DeleteForm(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "api/forms/delete/{id}")] HttpRequest req,
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

                var principal = MarsOfficePrincipal.Parse(req);
                var uid = principal.FindFirst("id").Value;


                // validate role
                if (!principal.FindAll("roles").Any(x => x.Value == "Admin" || x.Value == "Owner"))
                {
                    return new StatusCodeResult(401);
                }

                var formId = req.RouteValues["id"].ToString();

                var documentUri = UriFactory.CreateDocumentUri("forms", "Forms", formId);
                var entityResponse = await client.ReadDocumentAsync<FormEntity>(documentUri, new RequestOptions
                {
                    PartitionKey = new PartitionKey(uid)
                });
                if (entityResponse.Document == null)
                {
                    return new NotFoundResult();
                }
                var entity = entityResponse.Document;
                if (entity.UserId != uid)
                {
                    return new StatusCodeResult(401);
                }

                await client.DeleteDocumentAsync(documentUri, new RequestOptions
                {
                    PartitionKey = new PartitionKey(uid)
                });

                return new OkResult();
            }
            catch (Exception e)
            {
                log.LogError(e, "Exception occured in function");
                return new BadRequestObjectResult(Errors.Extract(e));
            }
        }

        private static Expression<Func<FormEntity, object>> GetPropertyExpression(string propertyName)
        {
            return propertyName switch
            {
                "title" => x => x.Title,
                "createdDate" => x => x.CreatedDate,
                "deadline" => x => x.Deadline,
                "userName" => x => x.UserName,
                _ => throw new Exception("forms.getForms.invalidSortColumn"),
            };
        }

        private async Task SendNotificationsForFormEntity(string myId, FormEntity entity, IAsyncCollector<RequestNotificationDto> outputNotifications, ILogger log, string templateName, bool sendEmail)
        {
            try
            {
                using var accessClient = _httpClientFactory.CreateClient("access");
                if (entity.FormAccesses?.Any() == true)
                {
                    var userDtos = new List<UserDto>();
                    foreach (var accessEntity in entity.FormAccesses)
                    {
                        var usersResponse = await accessClient.GetStringAsync("/api/access/getUsersByOrganisationId/" + accessEntity.OrganisationId + "?includeDetails=true");
                        var userDtosResponse = JsonConvert.DeserializeObject<IEnumerable<UserDto>>(usersResponse, new JsonSerializerSettings
                        {
                            ContractResolver = new CamelCasePropertyNamesContractResolver()
                        });
                        userDtos.AddRange(userDtosResponse);
                    }
                    userDtos = userDtos.DistinctBy(x => x.Id).Where(x => x.Id != myId).ToList();
                    if (!userDtos.Any())
                    {
                        return;
                    }
                    var batchSize = 100;
                    var noOfBatches = (int)Math.Ceiling(userDtos.Count * 1f / batchSize);

                    var notificationTypes = new List<NotificationType> {
                        NotificationType.InApp
                    };
                    if (sendEmail)
                    {
                        notificationTypes.Add(NotificationType.Email);
                    }

                    for (var i = 0; i < noOfBatches; i++)
                    {
                        var usersSlice = userDtos.Skip(i * batchSize).Take(batchSize).ToList();
                        await outputNotifications.AddAsync(new RequestNotificationDto
                        {
                            AbsoluteRouteUrl = "/forms/view/" + entity.Id,
                            NotificationTypes = notificationTypes,
                            PlaceholderData = new Dictionary<string, string> {
                                {"formName", entity.Title},
                                {"userName", entity.UserName},
                                {"link", "/forms/view/" + entity.Id}
                            },
                            Severity = Notifications.Abstractions.Severity.Info,
                            TemplateName = templateName,
                            Recipients = usersSlice.Select(u => new RecipientDto
                            {
                                Email = u.Email,
                                PreferredLanguage = u.UserPreferences?.PreferredLanguage,
                                UserId = u.Id
                            }).ToList()
                        });
                    }

                    await outputNotifications.FlushAsync();
                }
            }
            catch (Exception exc)
            {
                log.LogError(exc, "Notifications failed");
            }
        }
    }
}

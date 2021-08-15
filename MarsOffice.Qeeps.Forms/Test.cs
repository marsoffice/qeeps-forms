using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using MarsOffice.Qeeps.Access.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace MarsOffice.Qeeps.Forms
{
    public class Test
    {
        private readonly HttpClient _accessClient;
        public Test(IHttpClientFactory httpClientFactory)
        {
            _accessClient = httpClientFactory.CreateClient("access");
        }

        [FunctionName("Test")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/forms/test")] HttpRequest req)
        {
            var testResponse = await _accessClient.GetStringAsync("/api/access/test");
            var dto = JsonConvert.DeserializeObject<OrganisationDto>(testResponse, new JsonSerializerSettings {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            });
            return new OkObjectResult(dto);
        }
    }
}

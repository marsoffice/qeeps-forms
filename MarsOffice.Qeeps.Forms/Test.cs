using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System.Net.Http.Json;
using MarsOffice.Qeeps.Access.Abstractions;

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
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "test")] HttpRequest req)
        {
            var testResponse = await _accessClient.GetFromJsonAsync<OrganisationDto>("/api/access/test");
            return new OkObjectResult(testResponse);
        }
    }
}

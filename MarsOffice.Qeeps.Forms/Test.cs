using System.Security.Claims;
using System.Threading.Tasks;
using MarsOffice.Qeeps.Microfunction;
using MarsOffice.Qeeps.Notifications.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;

namespace MarsOffice.Qeeps.Forms
{
    public class Test
    {
        public Test()
        {
        }

        [FunctionName("Test")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/forms/test")] HttpRequest req,
            [ServiceBus("notifications", Connection = "sbconnectionstring")] IAsyncCollector<RequestNotificationDto> outputNotifications
            )
        {
            var principal = QeepsPrincipal.Parse(req);
            await outputNotifications.AddAsync(new RequestNotificationDto {
                AbsoluteRouteUrl = "/test",
                AdditionalData = new System.Collections.Generic.Dictionary<string, string> {
                    {"hi", "alin"}
                },
                NotificationTypes = new [] {NotificationType.InApp, NotificationType.Email},
                PreferredLanguage = "en",
                PlaceholderData = new System.Collections.Generic.Dictionary<string, string> {
                    {"link", "linkhere"},
                    {"formName", "formnamehere"}
                },
                TemplateName = "FormPosted",
                RecipientUserIds = new [] {principal.FindFirstValue("id")}
            });
            await outputNotifications.FlushAsync();
            return new OkObjectResult(new {a = 1});
        }
    }
}

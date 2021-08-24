using System;
using Newtonsoft.Json;

namespace MarsOffice.Qeeps.Forms.Entities
{
    public class FormAccessEntity
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        public string FormId { get; set; }
        public string OrganisationId { get; set; }
        public string FullOrganisationId { get; set; }
        public DateTime? SeenDate {get;set;}
    }
}
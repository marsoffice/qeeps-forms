using System;
using Newtonsoft.Json;

namespace MarsOffice.Qeeps.Forms.Entities
{
    public class FormAccessEntity
    {
        public string FormId { get; set; }
        public string OrganisationId { get; set; }
        public DateTime? SeenDate { get; set; }
    }
}
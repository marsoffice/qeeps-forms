using System;

namespace MarsOffice.Qeeps.Forms.Abstractions
{
    public class FormAccessDto
    {
        public string Id { get; set; }
        public string FormId { get; set; }
        public string OrganisationId { get; set; }
        public DateTime? SeenDate { get; set; }
    }
}
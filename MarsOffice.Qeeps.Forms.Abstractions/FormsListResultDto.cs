using System.Collections.Generic;

namespace MarsOffice.Qeeps.Forms.Abstractions
{
    public class FormsListResultDto
    {
        public IEnumerable<FormDto> Forms { get; set; }
        public int Total { get; set; }
    }
}
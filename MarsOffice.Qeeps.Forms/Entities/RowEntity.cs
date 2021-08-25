using System.Collections.Generic;

namespace MarsOffice.Qeeps.Forms.Entities
{
    public class RowEntity
    {
        public IEnumerable<string> Values { get; set; }
        public bool IsFrozen { get; set; }
    }
}
using System.Collections.Generic;
using MarsOffice.Qeeps.Forms.Abstractions;

namespace MarsOffice.Qeeps.Forms.Entities
{
    public class ColumnEntity
    {
        public string Name { get; set; }
        public bool IsRequired { get; set; }
        public IEnumerable<string> DropdownOptions { get; set; }
        public bool IsFrozen { get; set; }
        public bool IsHidden { get; set; }
        public ColumnDataType DataType { get; set; }
        public IEnumerable<string> AllowedExtensions { get; set; }
    }
}
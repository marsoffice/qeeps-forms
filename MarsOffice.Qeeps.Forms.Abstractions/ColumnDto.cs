using System.Collections.Generic;

namespace MarsOffice.Qeeps.Forms.Abstractions
{
    public class ColumnDto
    {
        public string Name { get; set; }
        public bool IsRequired { get; set; }
        public bool MultipleValues { get; set; }
        public IEnumerable<string> DropdownOptions { get; set; }
        public bool IsFrozen { get; set; }
        public bool IsHidden { get; set; }
        public ColumnDataType DataType { get; set; }
        public IEnumerable<string> AllowedExtensions { get; set; }
        public string Reference { get; set; }
        public dynamic Min { get; set; }
        public dynamic Max { get; set; }
        public int? MinLength { get; set; }
        public int MaxLength { get; set; }
    }
}
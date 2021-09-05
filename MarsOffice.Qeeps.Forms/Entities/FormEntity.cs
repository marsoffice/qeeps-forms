using System;
using System.Collections.Generic;
using MarsOffice.Qeeps.Files.Abstractions;
using Newtonsoft.Json;

namespace MarsOffice.Qeeps.Forms.Entities
{
    public class FormEntity
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        public string UserId { get; set; }
        public DateTime CreatedDate { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public IEnumerable<FileDto> Attachments { get; set; }
        public bool IsLocked { get; set; }
        public DateTime? LockedUntilDate { get; set; }
        public bool RowAppendDisabled { get; set; }
        public bool IsRecurrent { get; set; }
        public string CronExpression { get; set; }
        public bool IsPinned { get; set; }
        public DateTime? PinnedUntilDate { get; set; }
        public IEnumerable<string> Tags { get; set; }
        public IEnumerable<ColumnEntity> Columns { get; set; }
        public IEnumerable<IEnumerable<dynamic>> Rows { get; set; }
    }
}

using System;
using System.Collections.Generic;
using MarsOffice.Qeeps.Files.Abstractions;

namespace MarsOffice.Qeeps.Forms.Abstractions
{
    public class FormDto
    {
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
        public IEnumerable<ColumnDto> Columns { get; set; }
        public IEnumerable<IEnumerable<dynamic>> Rows { get; set; }

        public IEnumerable<FormAccessDto> FormAccesses { get; set; }
        public bool SendEmailNotifications { get; set; }
    }
}
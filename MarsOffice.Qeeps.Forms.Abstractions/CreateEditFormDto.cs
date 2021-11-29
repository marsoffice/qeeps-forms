namespace MarsOffice.Qeeps.Forms.Abstractions
{
    public class CreateEditFormDto
    {
        public FormDto Form { get; set; }
        public bool SendEmailNotifications { get; set; }
    }
}
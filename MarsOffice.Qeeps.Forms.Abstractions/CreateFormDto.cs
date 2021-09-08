namespace MarsOffice.Qeeps.Forms.Abstractions
{
    public class CreateFormDto
    {
        public FormDto Form { get; set; }
        public bool SendEmailNotifications { get; set; }
    }
}
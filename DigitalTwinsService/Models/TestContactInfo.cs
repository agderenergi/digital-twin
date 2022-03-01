namespace DigitalTwinsService.Models
{
    [DTModel("dtmi:test:TestContactInfo;1")]
    public class TestContactInfo
    {
        [DTModelContent("email", ContentType.Property)]
        public string Email { get; set; }
    }
}
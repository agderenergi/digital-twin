namespace DigitalTwinsService.Models
{
    [DTModel("dtmi:test:TestContactInfo;1")]
    public class TestContactInfo: TestBase
    {
        [DTModelContent("email", ContentType.Property)]
        public string Email { get; set; }
    }
}
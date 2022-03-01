namespace DigitalTwinsService.Models
{
    [DTModel("dtmi:test:TestBase;1")]
    public class TestBase
    {
        [DTModelContent("id", ContentType.Id)]
        public string Id { get; set; }
        
        [DTModelContent("createdBy", ContentType.Property)]
        public string CreatedBy { get; set; }
    }
}
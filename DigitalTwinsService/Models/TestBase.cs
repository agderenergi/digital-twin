namespace DigitalTwinsService.Models
{
    [DTModel("dtmi:test:TestBase;1")]
    public class TestBase
    {
        // this will be the @id for the twin
        public string Id { get; set; }
        
        [DTModelContent("createdBy", ContentType.Property)]
        public string CreatedBy { get; set; }
    }
}
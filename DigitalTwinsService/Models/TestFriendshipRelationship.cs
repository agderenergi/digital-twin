namespace DigitalTwinsService.Models
{
    public class TestFriendshipRelationship: DTRelationship
    {
        public override string RelationshipName { get; } = "isFriendOf";
        
        [DTModelContent("comment", ContentType.Property)]
        public string Comment { get; set; }
    }
}
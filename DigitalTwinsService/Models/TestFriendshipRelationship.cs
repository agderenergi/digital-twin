using Azure.DigitalTwins.Core;

namespace DigitalTwinsService.Models
{
    public class TestFriendshipRelationship: DTRelationship
    {
        public TestFriendshipRelationship(string relationshipName) : base(relationshipName)
        {
        }

        public TestFriendshipRelationship(BasicRelationship basicRelationship) : base(basicRelationship)
        {
        }
        
        [DTModelContent("comment", ContentType.Property)]
        public string Comment { get; set; }
    }
}
using System;
using System.Collections.Generic;

namespace DigitalTwinsService.Models
{
    public enum Gender
    {
        Female,
        Male,
        NonBinary
    }

    public class GeoLocation
    {
        [DTModelContent("latitude", ContentType.Property)]
        public double Latitude { get; set; }
        
        [DTModelContent("longitude", ContentType.Property)]
        public double Longitude { get; set; }
    }
    
    [DTModel("dtmi:test:TestPerson;1")]
    public class TestPerson: TestBase
    {
        [DTModelContent("name", ContentType.Property)]
        public string Name { get; set; }
        
        [DTModelContent("hasDriversLicence", ContentType.Property)]
        public bool HasDriversLicence { get; set; }
        
        [DTModelContent("birthDate", ContentType.Property)]
        public DateTimeOffset BirthDate { get; set; }
        
        [DTModelContent("carCount", ContentType.Property)]
        public int CarCount { get; set; }
        
        [DTModelContent("highScore", ContentType.Property)]
        public double HighScore { get; set; }
        
        [DTModelContent("timeSpentLookingAtCatVideos", ContentType.Property)]
        public TimeSpan TimeSpentLookingAtCatVideos { get; set; }
        
        [DTModelContent("height", ContentType.Property)]
        public double Height { get; set; }

        [DTModelContent("gender", ContentType.Property)]
        public Gender Gender { get; set; }
        
        [DTModelContent("geoLocation", ContentType.Object)]
        public GeoLocation GeoLocation { get; set; }
        
        [DTModelContent("contactInfo", ContentType.Component)]
        public TestContactInfo ContactInfo { get; set; }
        
        [DTModelContent("knows", ContentType.Relationship)]
        public List<DTRelationship> Friendships { get; set; }
    }
}
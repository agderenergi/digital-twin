using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DigitalTwinsService.Models;

namespace DigitalTwinsService
{
    public class ServiceTester
    {
        private readonly DigitalTwinsService _dtService;
        
        public ServiceTester(DigitalTwinsService dtService)
        {
            _dtService = dtService;
        }
        

        public async Task UploadADTModels()
        {
            var coreModels = Directory.GetFiles(@"Models/DTDL/", "*.json");            
            await _dtService.UploadModels(coreModels.ToList());
        }
        
        public async Task CreateTestTwins()
        {
            var testPerson1 = new TestPerson
            {
                Id = "Test_" + Guid.NewGuid(),
                Name = "Test1",
                Gender = Gender.Female,
                Height = 1.74,
                BirthDate = DateTimeOffset.Now.AddYears(-25),
                CarCount = 1,
                ContactInfo = new TestContactInfo { Id = "Test_" + Guid.NewGuid(), Email = "SomeEmail", CreatedBy = "SomeoneElse"},
                CreatedBy = "Someone",
                GeoLocation = new GeoLocation { Latitude = 51.1, Longitude = -0.5 },
                HighScore = 0,
                HasDriversLicence = true,
                TimeSpentLookingAtCatVideos = new TimeSpan(14, 2, 15)
            };

            var testPerson2 = new TestPerson
            {
                Id = "Test_" + Guid.NewGuid(),
                Name = "Test2",
                Gender = Gender.Male,
                Height = 1.74,
                BirthDate = DateTimeOffset.Now.AddYears(-25),
                CarCount = 1,
                ContactInfo = new TestContactInfo
                    { Id = "Test_" + Guid.NewGuid(), Email = "SomeEmail", CreatedBy = "SomeoneElse" },
                CreatedBy = "Someone",
                GeoLocation = new GeoLocation { Latitude = 51.1, Longitude = -0.5 },
                HighScore = 0,
                HasDriversLicence = true,
                TimeSpentLookingAtCatVideos = new TimeSpan(14, 2, 15),
                Friendships = new List<DTRelationship>
                {
                    new TestFriendshipRelationship
                    {
                        TargetId = testPerson1.Id,
                        Comment = "Old classmate"
                    }
                }
            };

            var twinPerson1 = DigitalTwinsService.ConvertToTwin(testPerson1.Id, testPerson1);
            var createdTwin1 = await _dtService.CreateOrReplaceTwin(twinPerson1);
            
            var twinPerson2 = DigitalTwinsService.ConvertToTwin(testPerson2.Id, testPerson2);
            var createdTwin2 = await _dtService.CreateOrReplaceTwin(twinPerson2);

            var relationship = await _dtService.CreateOrReplaceRelationship(testPerson2.Id, testPerson2.Friendships[0]);
        }
    }
}
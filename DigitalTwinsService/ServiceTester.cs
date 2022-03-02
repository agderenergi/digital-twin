using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Azure.DigitalTwins.Core;
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
                Id = "Test_6740cde7-961a-4e19-b914-be6cdd9450ea",
                Name = "Test1",
                Gender = Gender.Female,
                Height = 177,
                BirthDate = DateTimeOffset.Now.AddYears(-25),
                CarCount = 1,
                ContactInfo = new TestContactInfo { Email = "SomeEmail"},
                CreatedBy = "Someone",
                GeoLocation = new GeoLocation { Latitude = 51.1, Longitude = -0.5 },
                HighScore = 0,
                HasDriversLicence = true,
                TimeSpentLookingAtCatVideos = new TimeSpan(14, 2, 15)
            };

            var testPerson2 = new TestPerson
            {
                Id = "Test_693e3e41-d2db-45b4-91ad-2c4cbebe81bf",
                Name = "Test2",
                Gender = Gender.Male,
                Height = 174,
                BirthDate = DateTimeOffset.Now.AddYears(-45),
                CarCount = 3,
                // A sub component like ContactInfo cannot be null, but a "blank" instance is created in the TestPerson constructor
                CreatedBy = null,
                GeoLocation = null,
                HighScore = 0,
                HasDriversLicence = false,
                TimeSpentLookingAtCatVideos = new TimeSpan(14, 2, 15),
                Friendships = new List<TestFriendshipRelationship>
                {
                    new TestFriendshipRelationship("isFriendOf")
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

        /// <summary>
        /// Returns a twin represented as a BasicDigitalTwin
        /// </summary>
        public async Task<BasicDigitalTwin> GetTwin(string twinId) =>
            await _dtService.GetTwin(twinId);

        /// <summary>
        /// Returns a twin and its outgoing relationships parsed to a TestPerson C# Class
        /// </summary>
        public async Task<TestPerson> GetTestPerson(string twinId) =>
            await _dtService.GetCsObject<TestPerson>(twinId);

        public async Task<IEnumerable<TestPerson>> GetAllTestPersons() =>
            await _dtService.GetAllCsObjects<TestPerson>();

        public async Task DeleteTestTwins(List<TestPerson> testPersons)
        {
            foreach (var testPerson in testPersons)
            {
                foreach (var friendship in testPerson.Friendships)
                {
                    await _dtService.DeleteRelationship(testPerson.Id,
                        friendship.GetRelationshipId(testPerson.Id));
                }
            }

            foreach (var testPerson in testPersons)
                await _dtService.DeleteTwin(testPerson.Id);
        }
    }
}
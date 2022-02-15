using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Azure;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Microsoft.Azure.DigitalTwins.Parser;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace DigitalTwinsService
{
    public class DigitalTwinsService
    {
        private readonly IConfiguration _configuration;
        private DigitalTwinsClient _adtClient;
        
        public DigitalTwinsService(IConfiguration configuration)
        {
            _configuration = configuration;
            var useLocalAzureSignIn = _configuration.GetValue<bool>("ADT:UseLocalAzureSignIn");
            _adtClient = useLocalAzureSignIn ? CreateAdtClientUsingLocalAzureSignIn() : CreateAdtClientUsingClientIdAndSecret();
        }
        
        #region Create Client
        private DigitalTwinsClient CreateAdtClientUsingLocalAzureSignIn()
        {
            var credentials = new DefaultAzureCredential();
            var adtInstanceUrl = _configuration.GetValue<string>("ADT:InstanceURL");
            return new DigitalTwinsClient(new Uri(adtInstanceUrl), credentials);
        }
        
        private DigitalTwinsClient CreateAdtClientUsingClientIdAndSecret()
        {
            var adtClientId = _configuration.GetValue<string>("ADT:ClientId");
            var adtClientSecret = _configuration.GetValue<string>("ADT:ClientSecret");
            var adtTenantId = _configuration.GetValue<string>("ADT:TenantId");
            var credentials = new ClientSecretCredential(adtTenantId, adtClientId, adtClientSecret);

            var adtInstanceUrl = _configuration.GetValue<string>("ADT:InstanceURL");
            return new DigitalTwinsClient(new Uri(adtInstanceUrl), credentials);
        }
        #endregion
        
        #region Model Management
        public async Task UploadModels(List<string> modelFilenames)
        {
            var models = new List<string>();
            foreach (var filename in modelFilenames)
            {
                try
                {
                    models.Add(await File.ReadAllTextAsync(filename));
                }
                catch (Exception e)
                {
                    throw new ArgumentException($"UploadModel failed. Unable to read file {filename}.", e);
                }
            }

            var validationResult = await ValidateModels(models);
            if (validationResult != null)
            {
                throw new ArgumentException($"UploadModel failed. One or more model(s) contains invalid DTDL. Error: {validationResult}.");
            }

            var modelsToUpload = new List<string>();
            foreach (var model in models)
            {
                if (await CheckIfModelExists(model))
                {
                    Console.WriteLine($"Model {GetModelIdFromJson(model)} already exists. Skipping...");
                    continue;
                }
                modelsToUpload.Add(model);
            }

            if (!modelsToUpload.Any()) return;
            
            try
            {
                await _adtClient.CreateModelsAsync(modelsToUpload);
                Console.WriteLine($"Models successfully uploaded.");
            }
            catch (RequestFailedException rex)
            {
                throw new DigitalTwinsException($"UploadModel failed: {rex.Status}:{rex.Message}", rex.InnerException);
            }
        }

        public async Task<string> ValidateModels(List<string> models)
        {
            foreach (var model in models)
            {
                try
                {
                    JsonDocument.Parse(model);
                } 
                catch (Exception e)
                {
                    return $"Model file has invalid json. Json parser error \n{e.Message}";
                }            
            }
            
            var parser = new ModelParser { DtmiResolver = Resolver };
            try
            {
                await parser.ParseAsync(models);
            }
            catch (ParsingException pe)
            {
                var error = $"Models not valid: ";
                var derrCount = 1;
                foreach (var err in pe.Errors)
                {
                    error += $"Error {derrCount}: ";
                    error += $"{err.Message} ";
                    error += $"Primary ID: {err.PrimaryID} ";
                    error += $"Secondary ID: {err.SecondaryID} ";
                    error += $"Property: {err.Property}\n";
                    derrCount++;
                }

                return error;
            }
            catch (ResolutionException rex)
            {
                return rex.Message;
            }
            
            return null;
        }
        
        public async Task<DigitalTwinsModelData> GetModelById(string id)
        {
            try
            {
                var modelResponse = await _adtClient.GetModelAsync(id);
                return modelResponse.Value;
            }
            catch (RequestFailedException rex)
            {
                throw new DigitalTwinsException($"GetModelById failed: {rex.Status}:{rex.Message}", rex.InnerException);
            }
        }
        
        public async Task<List<DigitalTwinsModelData>> GetAllModels()
        {
            var models = new List<DigitalTwinsModelData>();
            try
            {
                var modelDataList = _adtClient.GetModelsAsync();
                await foreach (var md in modelDataList)
                    models.Add(md);
            }
            catch (RequestFailedException rex)
            {
                throw new DigitalTwinsException($"GetAllModels failed: {rex.Status}:{rex.Message}", rex.InnerException);
            }
        
            return models;
        }

        private async Task<bool> CheckIfModelExists(string model) =>
            (await GetAllModels()).SingleOrDefault(m => m.Id == GetModelIdFromJson(model)) != null;
        
        private static string GetModelIdFromJson(string jsonModel) =>
            JsonConvert.DeserializeObject<dynamic>(jsonModel)["@id"].ToString();

        private static async Task<IEnumerable<string>> Resolver(IReadOnlyCollection<Dtmi> dtmis)
        {
            await Console.Error.WriteAsync($"*** Error parsing models. Missing:");
            foreach (var d in dtmis)
            {
                await Console.Error.WriteAsync($"  {d}");
            }
            return null;
        }
        #endregion
        
        #region Twin Management
        public async Task<BasicDigitalTwin> CreateOrReplaceTwin(BasicDigitalTwin twin)
        {
            try
            {
                var response = await _adtClient.CreateOrReplaceDigitalTwinAsync(twin.Id, twin);
                return response.Value;
            }
            catch (RequestFailedException e)
            {
                throw new DigitalTwinsException($"CreateOrReplaceTwin failed for {twin.Id} : {twin.Metadata.ModelId}. Message: {e.Message}.", e);
            }
        }

        public async Task<BasicRelationship> CreateOrReplaceRelationship(string sourceId, DTRelationship dtRelationship)
        {
            try
            {
                var relId = $"{sourceId}-{dtRelationship.RelationshipName}->{dtRelationship.TargetId}";
                var response = await _adtClient.CreateOrReplaceRelationshipAsync(sourceId, relId,
                    dtRelationship.ToBasicRelationship(sourceId));
                return response.Value;
            }
            catch (RequestFailedException rex)
            {
                throw new DigitalTwinsException($"CreateOrReplaceRelation error: {rex.Status}:{rex.Message}", rex);
            }            
        }
        #endregion
        
        #region Helper Methods
        public static BasicDigitalTwin ConvertToTwin<T>(string twinId, T csObject)
        {
            var modelAttribute = csObject.GetType().GetCustomAttribute(typeof(DTModelAttribute), false) as DTModelAttribute;
            if (modelAttribute == null || string.IsNullOrEmpty(modelAttribute.ModelId))
                throw new Exception($"DTModelAttribute not set on {csObject.GetType().Name}");
            if (string.IsNullOrEmpty(twinId))
                throw new Exception($"Id not provided for {csObject.GetType().Name}");

            var twin = new BasicDigitalTwin
            {
                Metadata = { ModelId = modelAttribute.ModelId },
                Id = twinId,
                Contents = CreateTwinContents_Recursive(csObject)
            };
            
            return twin;
        }
        
        private static Dictionary<string, object> CreateTwinContents_Recursive<T>(T csObject)
        {
            var contents = new Dictionary<string, object>();
            var propInfos = csObject.GetType().GetProperties();
            foreach (var propInfo in propInfos)
            {
                var dtPropertyAttribute = propInfo.GetCustomAttribute(typeof(DTModelContentAttribute), true) as DTModelContentAttribute;
                var value = propInfo.GetValue(csObject);
                
                if (value == null)
                {
                    if (propInfo.PropertyType.IsValueType)
                        value = Activator.CreateInstance(propInfo.PropertyType);
                    else if (propInfo.PropertyType == typeof(string))
                        value = "";
                }

                if (propInfo.PropertyType.IsEnum)
                    value = value?.ToString();
                
                switch (dtPropertyAttribute)
                {
                    case { ContentType: ContentType.Property }:
                        contents.Add(dtPropertyAttribute.ContentName, value);
                        break;
                    case { ContentType: ContentType.Object }:
                        contents.Add(dtPropertyAttribute.ContentName, CreateTwinContents_Recursive(value));
                        break;
                    case { ContentType: ContentType.Component }:
                        var subComp = CreateTwinContents_Recursive(value);
                        subComp.Add(DigitalTwinsJsonPropertyNames.DigitalTwinMetadata, new object());
                        contents.Add(dtPropertyAttribute.ContentName, subComp);
                        break;
                }
            }

            return contents;
        }
        
        #endregion
    }
}
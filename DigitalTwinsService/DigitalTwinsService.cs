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
        /// <summary>
        /// This will read the contents of the DTDL json file paths, then validate the contents.
        /// Models already uploaded will be skipped, new ones will be uploaded. To update existing models
        /// either first delete the old or update the version number in the model Id.
        /// </summary>
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

        /// <summary>
        /// The list of models will first be checked for valid JSON formatting, then that they contain valid DTDL.
        /// If models have sub components or extend other models, all relevant models must be included.
        /// </summary>
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
        
        /// <summary>
        /// Returns a model matching the given model ID. If the Id
        /// is not a properly formatted DTDL Id or the model (including the version number)
        /// does not exist, a DigitalTwinsException will be thrown
        /// </summary>
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
        
        /// <summary>
        /// Returns all models currently stored on this digital twins instance.
        /// </summary>
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

        /// <summary>
        /// A decommissioned model can not be used to create new digital twins, but does not affect existing twins using that model.
        /// </summary>
        public async Task DecomissionModel(string modelId)
        {
            try
            {
                await _adtClient.DecommissionModelAsync(modelId);
            }
            catch (RequestFailedException rex)
            {
                throw new DigitalTwinsException($"DecomissionModel for model Id {modelId} failed: {rex.Status}:{rex.Message}", rex.InnerException);
            }
        }

        /// <summary>
        /// A model must be deleted before an updated DTDL file with same version number can be uploaded. Deleting a model
        /// will not delete existing twins, but they are now considered orphaned. Such twins can still be read, but properties can not
        /// be updated. Orphaned twins can still be queried by their "old" model Id.
        /// </summary>
        public async Task DeleteModel(string modelId)
        {
            try
            {
                await _adtClient.DeleteModelAsync(modelId);
            }
            catch (RequestFailedException rex)
            {
                throw new DigitalTwinsException($"DeleteModel with model Id {modelId} failed: {rex.Status}:{rex.Message}", rex.InnerException);
            }
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
        /// <summary>
        /// This will Upsert a twin described as a BasicDigitalTwin helper Class. For a suggestion on how to convert
        /// a C# class to a Basic Digital Twin, see the ConvertToTwin() method in the Helper Methods section below
        /// and the DTModel/DTModelContent custom attributes.
        /// </summary>
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

        /// <summary>
        /// Upserts a Relationship between two twins that already exists via a DTRelationship base class. Optional attributes
        /// for the relationship is supported (see "TestFriendshipRelationship.cs" for an example).
        /// </summary>
        public async Task<BasicRelationship> CreateOrReplaceRelationship(string sourceId, DTRelationship dtRelationship)
        {
            try
            {
                var relId = dtRelationship.GetRelationshipId(sourceId);
                var response = await _adtClient.CreateOrReplaceRelationshipAsync(sourceId, relId,
                    dtRelationship.ToBasicRelationship(sourceId));
                return response.Value;
            }
            catch (RequestFailedException rex)
            {
                throw new DigitalTwinsException($"CreateOrReplaceRelation error: {rex.Status}:{rex.Message}", rex);
            }            
        }

        /// <summary>
        /// Deletes an outgoing relationship given a sourceId and the id of the relationship
        /// </summary>
        public async Task DeleteRelationship(string sourceTwinId, string relationshipId)
        {
            try
            {
                await _adtClient.DeleteRelationshipAsync(sourceTwinId, relationshipId);
            }
            catch (Exception e)
            {
                throw new DigitalTwinsException(
                    $"Deleting relationship for twin {sourceTwinId} and relationshipId {relationshipId} failed.", e);
            }
        }

        /// <summary>
        /// Gets a single twin instance as a BasicDigitalTwin helper class by its $dtId
        /// </summary>
        public async Task<BasicDigitalTwin> GetTwin(string twinId)
        {
            try
            {
                var result = await _adtClient.GetDigitalTwinAsync<BasicDigitalTwin>(twinId);
                return result.Value;
            }
            catch (Exception e)
            {
                throw new DigitalTwinsException($"GetTwin with Id {twinId} failed.", e);
            }
        }

        public async Task DeleteTwin(string twinId)
        {
            try
            {
                await _adtClient.DeleteDigitalTwinAsync(twinId);
            }
            catch (Exception e)
            {
                throw new DigitalTwinsException(
                    $"Deleting twin {twinId} failed.", e);
            }
        }

        /// <summary>
        /// Returns all twin instances for a specific model type and version id as a BasicDigitalTwin. Note that this will not
        /// include any relationships.
        /// </summary>
        public async Task<List<BasicDigitalTwin>> GetAllTwinsOfModelType(string modelId)
        {
            var twins = new List<BasicDigitalTwin>();

            try
            {
                var result =
                    _adtClient.QueryAsync<BasicDigitalTwin>(
                        $"SELECT * FROM digitaltwins WHERE IS_OF_MODEL('{modelId}')");
                await foreach (var twin in result)
                    twins.Add(twin);
            }
            catch (Exception e)
            {
                throw new DigitalTwinsException("Digital twins query failed.", e);
            }

            return twins;
        }

        /// <summary>
        /// Returns all outgoing relationships for a twin instance (i.e. relationships with the given twin id as 'SourceId')
        /// </summary>
        public async Task<List<BasicRelationship>> GetOutgoingRelationships(string twinId)
        {
            var relationships = new List<BasicRelationship>();
            try
            {
                var results = _adtClient.GetRelationshipsAsync<BasicRelationship>(twinId);
                await foreach (var rel in results)
                {
                    relationships.Add(rel);
                }
            }
            catch (RequestFailedException rex)
            {
                throw new DigitalTwinsException($"Relationship retrieval error: {rex.Status}:{rex.Message}", rex);
            }

            return relationships;
        }
        
        /// <summary>
        /// Gets a twin instance with a given $dtId and parses it to a given C# Class with DTModel/DTModelContent attributes.
        /// Outgoing relationships are included.
        /// </summary>
        public async Task<T> GetCsObject<T>(string twinId) where T : new()
        {
            var basicTwin = await GetTwin(twinId);
            var outgoingRelationships = await GetOutgoingRelationships(twinId);
            
            return ConvertFromTwin<T>(basicTwin, outgoingRelationships);
        }

        /// <summary>
        /// Gets all twin instances matching the DTDL ModelId referenced in the given C# Class' DTModelAttribute. Outgoing
        /// relationships are included.
        /// </summary>
        public async Task<IEnumerable<T>> GetAllCsObjects<T>() where T : new()
        {
            var modelAttribute = (DTModelAttribute)typeof(T).GetCustomAttribute(typeof(DTModelAttribute));
            if (modelAttribute == null)
                throw new ArgumentException($"Generic type {typeof(T).Name} does not have a DTModelAttribute.");
            
            var twins = await GetAllTwinsOfModelType(modelAttribute.ModelId);

            var csObjects = new List<T>();
            foreach (var twin in twins)
            {
                var rels = await GetOutgoingRelationships(twin.Id);
                csObjects.Add(ConvertFromTwin<T>(twin, rels));
            }

            return csObjects;
        }
        
        #endregion
        
        #region Helper Methods
        /// <summary>
        /// This utility method converts any instance of a C# class decorated with the DTModel/DTModelContent attributes to a
        /// BasicDigitalTwin helper class that can be inserted/updated. See "TestPerson.cs" and its related classes for
        /// an example.
        /// </summary>
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
        
        /// <summary>
        /// Converts a basic twin and its outgoing relationships to a given C# Class decorated with DTMOdel/DTModelContent attributes
        /// </summary>
        public T ConvertFromTwin<T>(BasicDigitalTwin twin, List<BasicRelationship> relationships) where T : new()
        {
            var modelAttribute = typeof(T).GetCustomAttribute(typeof(DTModelAttribute), false) as DTModelAttribute;
            if (modelAttribute == null || string.IsNullOrEmpty(modelAttribute.ModelId))
                throw new Exception($"DTModelAttribute not set on {typeof(T).Name}");

            var csObject = ParseTwinContents<T>(twin.Contents, relationships);
            var idPropInfo =
                typeof(T).GetProperties()
                    .SingleOrDefault(prop =>
                        ((DTModelContentAttribute)prop.GetCustomAttribute(typeof(DTModelContentAttribute)))?
                        .ContentType == ContentType.Id);
            if (idPropInfo != null)
                idPropInfo.SetValue(csObject, twin.Id);
    
            return csObject;
        }

        private T ParseTwinContents<T>(IDictionary<string, object> twinContents, List<BasicRelationship> relationships) where T : new() =>
            (T)ParseTwinContents_Recursive(typeof(T), twinContents, relationships);

        private object ParseTwinContents_Recursive(Type type, IDictionary<string, object> twinContents,
            List<BasicRelationship> relationships)
        {
            var csObject = Activator.CreateInstance(type);
            var propInfos = csObject.GetType().GetProperties();
            foreach (var propInfo in propInfos)
            {
                var dtPropertyAttribute =
                    propInfo.GetCustomAttribute(typeof(DTModelContentAttribute), true) as DTModelContentAttribute;
                if (dtPropertyAttribute == null || dtPropertyAttribute.ContentType == ContentType.Id)
                    continue;
                
                if (dtPropertyAttribute.ContentType == ContentType.Relationship)
                {
                    if (propInfo.GetValue(csObject) == null)
                        propInfo.SetValue(csObject, Activator.CreateInstance(propInfo.PropertyType));
                    if (relationships == null ||
                        !relationships.Any(rel => rel.Name.Equals(dtPropertyAttribute.ContentName)))
                        continue;
                    
                    foreach (var rel in relationships.Where(rel => rel.Name.Equals(dtPropertyAttribute.ContentName)))
                    {
                        var dtRelationship = Activator.CreateInstance(propInfo.PropertyType.GenericTypeArguments[0],
                            rel);
                        var methodInfo = propInfo.PropertyType.GetMethod("Add");
                        methodInfo.Invoke(propInfo.GetValue(csObject), new[] {dtRelationship});
                    }
                    continue;
                }
                
                if (!twinContents.ContainsKey(dtPropertyAttribute.ContentName))
                    continue;

                object twinValue;
                if (dtPropertyAttribute.ContentType == ContentType.Object || dtPropertyAttribute.ContentType == ContentType.Component)
                {
                    var subContent =
                        (Dictionary<string, object>)((JsonElement)twinContents[dtPropertyAttribute.ContentName])
                        .Deserialize(
                            typeof(IDictionary<string, object>));
                    twinValue = ParseTwinContents_Recursive(propInfo.PropertyType, subContent, relationships);
                    propInfo.SetValue(csObject, twinValue);
                    continue;
                }

                
                var jsonElement = (JsonElement)twinContents[dtPropertyAttribute.ContentName];
                twinValue = propInfo.PropertyType.IsEnum
                    ? Enum.Parse(propInfo.PropertyType, jsonElement.ToString())
                    : jsonElement.Deserialize(propInfo.PropertyType);
                
                propInfo.SetValue(csObject, twinValue);
            }

            return csObject;
        }
        
        private static Dictionary<string, object> CreateTwinContents_Recursive<T>(T csObject)
        {
            var contents = new Dictionary<string, object>();
            var propInfos = csObject.GetType().GetProperties();
            foreach (var propInfo in propInfos)
            {
                var dtPropertyAttribute = propInfo.GetCustomAttribute(typeof(DTModelContentAttribute), true) as DTModelContentAttribute;
                if (dtPropertyAttribute == null)
                    continue;
                
                var value = propInfo.GetValue(csObject);
                if (value == null)
                {
                    if (dtPropertyAttribute.ContentType == ContentType.Component)
                        throw new DigitalTwinsException($"Subcomponent {propInfo.Name} cannot be null.");
                    
                    continue;
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
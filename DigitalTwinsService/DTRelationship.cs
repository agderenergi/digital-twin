using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Azure.DigitalTwins.Core;

namespace DigitalTwinsService
{
    public abstract class DTRelationship
    {
        public DTRelationship(string relationshipName)
        {
            RelationshipName = relationshipName;
        }

        public DTRelationship(BasicRelationship basicRelationship)
        {
            RelationshipName = basicRelationship.Name;
            TargetId = basicRelationship.TargetId;
            var propInfos = this.GetType().GetProperties();

            foreach (var propInfo in propInfos)
            {
                var dtPropertyAttribute =
                    propInfo.GetCustomAttribute(typeof(DTModelContentAttribute), true) as DTModelContentAttribute;
                if (dtPropertyAttribute == null)
                    continue;
                
                if (!basicRelationship.Properties.ContainsKey(dtPropertyAttribute.ContentName))
                    continue;
                
                var jsonElement = (JsonElement)basicRelationship.Properties[dtPropertyAttribute.ContentName];
                var propValue = propInfo.PropertyType.IsEnum
                    ? Enum.Parse(propInfo.PropertyType, jsonElement.ToString())
                    : jsonElement.Deserialize(propInfo.PropertyType);
                
                propInfo.SetValue(this, propValue);
            }
        }
        
        public string RelationshipName { get; set; }

        public string GetRelationshipId(string sourceId) =>
            $"{sourceId}-{RelationshipName}->{TargetId}";

        public string TargetId { get; set; }
        
        public BasicRelationship ToBasicRelationship(string sourceId) => 
            new BasicRelationship
        {
            SourceId = sourceId,
            TargetId = TargetId,
            Name = RelationshipName,
            Properties = GetRelationshipProperties()
        };

        private Dictionary<string, object> GetRelationshipProperties()
        {
            var properties = new Dictionary<string, object>();
            var propInfos = GetType().GetProperties();
            foreach (var propInfo in propInfos)
            {
                var modelAttribute = propInfo.GetCustomAttribute(typeof(DTModelContentAttribute), true) as DTModelContentAttribute;
                if (modelAttribute != null)
                    properties.Add(modelAttribute.ContentName, propInfo.GetValue(this));
            }
            return properties;
        }
    }
}
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Azure.DigitalTwins.Core;

namespace DigitalTwinsService
{
    public abstract class DTRelationship
    {
        public abstract string RelationshipName { get; }
        
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
using System;

namespace DigitalTwinsService
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class DTModelAttribute: Attribute
    {

        public DTModelAttribute(string modelId) => ModelId = modelId;
        
        public string ModelId { get; }
    }
}
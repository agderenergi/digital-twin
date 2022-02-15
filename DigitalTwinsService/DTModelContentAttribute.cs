using System;

namespace DigitalTwinsService
{
    public enum ContentType {
        Property,
        Component,
        Object,
        Relationship
    }
    
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public class DTModelContentAttribute : Attribute
    {
        public DTModelContentAttribute(string contentName, ContentType contentType)
        {
            ContentName = contentName;
            ContentType = contentType;
        }
        
        public string ContentName { get; }
        
        public ContentType ContentType { get; }
    }
}
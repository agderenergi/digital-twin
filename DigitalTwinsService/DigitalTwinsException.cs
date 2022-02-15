using System;

namespace DigitalTwinsService
{
    public class DigitalTwinsException: Exception
    {
        public DigitalTwinsException() { }

        public DigitalTwinsException(string message) : base(message) { }

        public DigitalTwinsException(string message, Exception inner) : base(message, inner) { }       
    }
}
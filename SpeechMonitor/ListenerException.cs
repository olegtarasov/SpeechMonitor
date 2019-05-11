using System;

namespace SpeechMonitor
{
    public class ListenerException : Exception
    {
        public ListenerException(string message) : base(message)
        {
        }

        public ListenerException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
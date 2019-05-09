using System;

namespace VadnetSharp
{
    public class SpeechEndedEventArgs : EventArgs
    {
        public SpeechEndedEventArgs(DateTime endTime, TimeSpan span)
        {
            Span = span;
            EndTime = endTime;
        }

        public DateTime EndTime { get; set; }
        public TimeSpan Span { get; set; }
    }
}
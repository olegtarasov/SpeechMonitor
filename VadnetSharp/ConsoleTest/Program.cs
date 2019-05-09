using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using VadnetSharp;

namespace ConsoleTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            var cloud = new YandexCloud();
            var listener = new VadListener();
            var monitor = new AudioMonitor();

            cloud.RefreshToken("");

            listener.SpeechStarted += (sender, eventArgs) =>
                Console.WriteLine("Speech started");
            listener.SpeechEnded += (sender, e) =>
            {
                Console.WriteLine($"Speech ended. Duration: {e.Span}");
                var ogg = monitor.GetOpusTimeframe(e.EndTime, e.Span);
                Console.WriteLine(cloud.RecognizeText(ogg));
                //File.WriteAllBytes("capture.ogg", ogg);
                //monitor.WriteTimeframeToFile("capture.wav", e.EndTime, e.Span);
            };

            monitor.BeginRecording();
            listener.BeginListen();
            Console.WriteLine("Hello World!");
            Console.ReadKey();
        }

        private static void TestList()
        {
            var list = new LinkedList<int>();

            list.AddLast(1);
            list.AddLast(2);
            list.AddLast(3);
            list.AddLast(4);

            var first = list.First;
            list.RemoveFirst();
        }
    }
}
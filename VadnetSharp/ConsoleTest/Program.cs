using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using NAudio.Wave;
using VadnetSharp;

namespace ConsoleTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            var listener = new VadListener();
            var monitor = new AudioMonitor();

            listener.SpeechStarted += (sender, eventArgs) =>
                Console.WriteLine("Speech started");
            listener.SpeechEnded += (sender, e) =>
            {
                Console.WriteLine($"Speech ended. Duration: {e.Span}");
                var data = monitor.GetTimeframe(e.EndTime, e.Span);
                Console.WriteLine("Playing back...");
                PlayPCM(data, monitor.WaveFormat);
                Console.WriteLine("Playback finished.");
            };

            monitor.BeginRecording();
            listener.BeginListen();
            Console.WriteLine("Hello World!");
            Console.ReadKey();
        }

        private static void PlayPCM(byte[] data, WaveFormat format)
        {
            var evt = new ManualResetEventSlim();
            var waveOut = new WaveOutEvent();
            var provider = new RawSourceWaveStream(data, 0, data.Length, format);

            waveOut.PlaybackStopped += (sender, args) => evt.Set();
            waveOut.Init(provider);
            waveOut.Play();

            evt.Wait();

            provider.Dispose();
            waveOut.Dispose();
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CLAP;
using NAudio.Wave;
using Serilog;
using SpeechMonitor;

namespace ConsoleTest
{
    internal class Program
    {
        internal static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.Unicode;

            var config = new LoggerConfiguration()
                         .MinimumLevel.Debug()
                         .WriteTo.Console();

            Log.Logger = config.CreateLogger();

            Parser.Run(args, new App());
        }
    }

    public class App
    {
        private static readonly ILogger log = Log.ForContext<App>();

        [Verb(IsDefault = true)]
        public void TestMonitor()
        {
            using var listener = new SpeechListener();
            using var monitor = new AudioBufferRecorder();

            listener.SpeechStarted += (sender, eventArgs) =>
                log.Information("Speech started");
            listener.SpeechEnded += (sender, e) =>
            {
                log.Information($"Speech ended. Duration: {e.Span}");
                var data = monitor.GetTimeframe(e.EndTime, e.Span);

                log.Information("Playing back...");
                listener.Pause();
                PlayPCM(data, monitor.WaveFormat);
                listener.Resume();
                log.Information("Playback finished.");
            };

            monitor.BeginRecording();
            listener.BeginListen();

            log.Information("Speak. Press any key to exit.");
            Console.ReadKey();
        }

        private void PlayPCM(byte[] data, WaveFormat format)
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
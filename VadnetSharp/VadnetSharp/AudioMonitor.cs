using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Concentus.Enums;
using Concentus.Oggfile;
using Concentus.Structs;
using NAudio.Wave;

namespace VadnetSharp
{
    public sealed class AudioMonitor : IDisposable
    {
        private readonly long _maxLen;
        private readonly WaveInEvent _monitor;
        private readonly LinkedList<byte[]> _chunks = new LinkedList<byte[]>();

        private long _totalLen = 0;
        private bool _disposed = false;

        public AudioMonitor(int maxSeconds = 120)
        {
            _monitor = new WaveInEvent();

            _monitor.WaveFormat = new WaveFormat(48000, 16, 1);

            _maxLen = _monitor.WaveFormat.AverageBytesPerSecond * maxSeconds;
        }

        public void BeginRecording()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(AudioMonitor));
            }

            _monitor.DataAvailable += MonitorOnDataAvailable;
            _monitor.StartRecording();
        }

        public byte[] GetTimeframe(DateTime endTime, TimeSpan span)
        {
            var last = _chunks.Last;
            var now = DateTime.Now;
            int delta = (int)Math.Ceiling(span.TotalSeconds + (now - endTime).TotalSeconds);
            int deltaLen = _monitor.WaveFormat.AverageBytesPerSecond * delta;

            var cur = last;
            int curLen = 0;
            var frame = new LinkedList<byte[]>();
            while (cur != null)
            {
                curLen += cur.Value.Length;
                frame.AddFirst(cur.Value);
                if (curLen >= deltaLen)
                {
                    break;
                }
                cur = cur.Previous;
            }

            using (var stream = new MemoryStream((int)(deltaLen * 1.1)))
            {
                foreach (var segment in frame)
                {
                    stream.Write(segment, 0, segment.Length);
                }

                return stream.ToArray();
            }
        }

        public byte[] GetOpusTimeframe(DateTime endTime, TimeSpan span)
        {
            //var data = GetTimeframe(endTime, span);
            //using (var oggStream = new MemoryStream())
            //{
            //    var encoder = new OpusEncoder(48000, 1, OpusApplication.OPUS_APPLICATION_AUDIO);
            //    encoder.ForceMode = OpusMode.MODE_SILK_ONLY;
            //    //encoder.Bitrate = 64000;
            //    var oggOut = new OpusOggWriteStream(encoder, oggStream);

            //    var shorts = BytesToShorts(data);
            //    oggOut.WriteSamples(shorts, 0, shorts.Length);
            //    oggOut.Finish();

            //    return oggStream.ToArray();
            //}
            string pcmFile = Path.GetTempFileName();
            string oggFile = Path.GetTempFileName();
            WriteTimeframeToFile(pcmFile, endTime, span);
            Process.Start("opusenc.exe", $"{pcmFile} {oggFile}").WaitForExit();
            var result = File.ReadAllBytes(oggFile);

            File.Delete(pcmFile);
            File.Delete(oggFile);

            return result;
            //var data = GetTimeframe(endTime, span);
            //using (var input = new MemoryStream())
            //using (var waveWriter = new WaveFileWriter(input, _monitor.WaveFormat))
            //using (var output = new MemoryStream())
            //using (var error = new MemoryStream())
            //{
            //    try
            //    {
            //        waveWriter.Write(data, 0, data.Length);
            //        waveWriter.Flush();

            //        input.Seek(0, SeekOrigin.Begin);

            //        var info = new ProcessStartInfo("opusenc.exe", "- -")
            //        {
            //            RedirectStandardInput = true,
            //            RedirectStandardOutput = true,
            //            //RedirectStandardError = true,
            //            UseShellExecute = false
            //        };

            //        var proc = Process.Start(info);
            //        input.CopyTo(proc.StandardInput.BaseStream);
            //        proc.StandardInput.Close();
            //        proc.StandardOutput.BaseStream.CopyTo(output);
            //        //proc.StandardError.BaseStream.CopyTo(error);
            //        proc.WaitForExit();

            //        string errorText = Encoding.UTF8.GetString(error.ToArray());

            //        return output.ToArray();
            //    }
            //    catch (Exception e)
            //    {
            //        throw;
            //    }
            //}
            
            
            //proc.WaitForExit();

            //return File.ReadAllBytes("file.ogg");
        }

        public void WriteTimeframeToFile(string file, DateTime endTime, TimeSpan span)
        {
            var data = GetTimeframe(endTime, span);
            using (var writer = new WaveFileWriter(file, _monitor.WaveFormat))
            {
                writer.Write(data, 0, data.Length);
            }
        }

        private void MonitorOnDataAvailable(object sender, WaveInEventArgs e)
        {
            var chunk = new byte[e.BytesRecorded];
            Array.Copy(e.Buffer, chunk, e.BytesRecorded);

            _chunks.AddLast(chunk);
            _totalLen += e.BytesRecorded;

            if (_totalLen > _maxLen)
            {
                _totalLen -= _chunks.First.Value.Length;
                _chunks.RemoveFirst();
            }
        }

        /// <summary>
        /// Converts interleaved byte samples (such as what you get from a capture device)
        /// into linear short samples (that are much easier to work with)
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private static short[] BytesToShorts(byte[] input)
        {
            return BytesToShorts(input, 0, input.Length);
        }

        /// <summary>
        /// Converts interleaved byte samples (such as what you get from a capture device)
        /// into linear short samples (that are much easier to work with)
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private static short[] BytesToShorts(byte[] input, int offset, int length)
        {
            short[] processedValues = new short[length / 2];
            for (int c = 0; c < processedValues.Length; c++)
            {
                processedValues[c] = (short)(((int)input[(c * 2) + offset]) << 0);
                processedValues[c] += (short)(((int)input[(c * 2) + 1 + offset]) << 8);
            }

            return processedValues;
        }

        public void Dispose()
        {
            _disposed = true;
            _monitor.StopRecording();
            _monitor.Dispose();
        }
    }
}
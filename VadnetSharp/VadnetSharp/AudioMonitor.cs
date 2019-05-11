using System;
using System.Collections.Generic;
using System.IO;
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

        public WaveFormat WaveFormat => _monitor.WaveFormat;

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

        public void Dispose()
        {
            _disposed = true;
            _monitor.StopRecording();
            _monitor.Dispose();
        }
    }
}
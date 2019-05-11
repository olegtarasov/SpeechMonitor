using System;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace VadnetSharp
{
    public sealed class VadListener : IDisposable
    {
        private const string VoiceOpen = "<voice>";
        private const string VoiceClose = "</voice>";

        private readonly UdpClient _client;
        private readonly float _speechThreshold;

        private bool _disposed = false;
        private bool _isSpeaking = false;
        private DateTime _startTime;

        public event EventHandler SpeechStarted;
        public event EventHandler<SpeechEndedEventArgs> SpeechEnded;

        public VadListener(float speechThreshold = 0.85f)
        {
            _speechThreshold = speechThreshold;
            _client = new UdpClient(12354);
        }

        public void BeginListen()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(VadListener));
            }

            Task.Run(async () =>
            {
                while (!_disposed)
                {
                    var result = await _client.ReceiveAsync();
                    if (result.Buffer.Length == 0)
                    {
                        continue;
                    }

                    string content = Encoding.UTF8.GetString(result.Buffer);
                    ProcessXml(content);
                }
            });
        }

        public void Dispose()
        {
            _disposed = true;
            _client.Dispose();
        }

        private void ProcessXml(string xml)
        {
            int voiceStartPos = xml.IndexOf(VoiceOpen);
            if (voiceStartPos < 0)
            {
                return;
            }

            voiceStartPos += VoiceOpen.Length;

            int voiceEndPos = xml.IndexOf(VoiceClose);
            if (voiceEndPos < 0)
            {
                return;
            }

            string voiceProbText = xml.Substring(voiceStartPos, voiceEndPos - voiceStartPos);
            if (string.IsNullOrEmpty(voiceProbText))
            {
                return;
            }

            if (!float.TryParse(voiceProbText, NumberStyles.Any, NumberFormatInfo.InvariantInfo, out float voiceProb))
            {
                return;
            }

            if (voiceProb >= _speechThreshold)
            {
                if (_isSpeaking)
                {
                    return;
                }

                _startTime = DateTime.Now;
                _isSpeaking = true;
                OnSpeechStarted();
            }
            else
            {
                if (!_isSpeaking)
                {
                    return;
                }

                _isSpeaking = false;
                var endTime = DateTime.Now;
                var span = (endTime - _startTime) + TimeSpan.FromSeconds(0.5d);
                OnSpeechEnded(new SpeechEndedEventArgs(endTime, span));
            }
        }

        private void OnSpeechStarted()
        {
            SpeechStarted?.Invoke(this, EventArgs.Empty);
        }

        private void OnSpeechEnded(SpeechEndedEventArgs e)
        {
            SpeechEnded?.Invoke(this, e);
        }
    }
}

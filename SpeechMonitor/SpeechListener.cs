using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SpeechMonitor.Logging;

namespace SpeechMonitor
{
    public sealed class SpeechListener : IDisposable
    {
        private const string VoiceOpen = "<voice>";
        private const string VoiceClose = "</voice>";

        private static readonly ILog _log = LogProvider.For<SpeechListener>();

        private readonly UdpClient _client;
        private readonly float _speechThreshold;

        private readonly BlockingCollection<string> _queue = new BlockingCollection<string>(new ConcurrentQueue<string>());
        private readonly CancellationTokenSource _processorCancellation = new CancellationTokenSource();

        private bool _disposed = false;
        private bool _isSpeaking = false;
        private DateTime _startTime;
        private Process _process;
        private Timer _timer;
        private bool _isPaused;

        public event EventHandler SpeechStarted;
        public event EventHandler<SpeechEndedEventArgs> SpeechEnded;

        public SpeechListener(float speechThreshold = 0.75f)
        {
            _speechThreshold = speechThreshold;
            _client = new UdpClient(12354);
        }

        /// <summary>
        /// Begins listening for speech.
        /// </summary>
        /// <exception cref="ObjectDisposedException">When object has been disposed.</exception>
        /// <exception cref="ListenerException">When there is an exception.</exception>
        public void BeginListen()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SpeechListener));
            }

            new Thread(ProcessMessages).Start();

            StartVadNetAsync();

            _log.Info("SSI started.");

            Task.Run(async () =>
            {
                while (!_disposed)
                {
                    var result = await _client.ReceiveAsync();
                    if (result.Buffer.Length == 0 || _isPaused)
                    {
                        continue;
                    }

                    string content = Encoding.UTF8.GetString(result.Buffer);
                    _queue.TryAdd(content);
                }
            });
        }

        public void Pause()
        {
            _isPaused = true;
            _isSpeaking = false;
        }

        public void Resume()
        {
            Task.Run(async () =>
            {
                await Task.Delay(1000);
                _isPaused = false;
            });
        }

        public void Dispose()
        {
            _disposed = true;
            _timer.Dispose();
            _process?.Kill();
            _client.Dispose();
            _processorCancellation.Cancel();
        }

        private void ProcessMessages()
        {
            try
            {
                foreach (var item in _queue.GetConsumingEnumerable(_processorCancellation.Token))
                {
                    ProcessXml(item);
                }
            }
            catch (OperationCanceledException)
            {
                _log.Info("Processor thread finished.");
            }
            catch (Exception e)
            {
                _log.Error(e, "Unhandled exception in queue processor!");
            }
        }


        private void CheckSsi(object state)
        {
            if (_process != null && _process.HasExited)
            {
                _process = null;
                _timer.Dispose();
                _log.Warn("SSI process has exited. Restarting.");
                StartVadNetAsync();
            }
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

        private void StartVadNetAsync()
        {
            string ssiPath = GetFilePath(Path.Combine("vadnet", "bin", "xmlpipe.exe"));
            string workDir = GetFilePath(Path.Combine("vadnet"));

            _log.Info($"Starting SSI: {ssiPath}");

            if (!File.Exists(ssiPath))
            {
                InitializeSsi();
            }

            var startInfo = new ProcessStartInfo(ssiPath, "-log ssi.log -confstr \"audio:live=True;audio:live:mic=True;send:do=True\" -config vad vad")
                            {
                                UseShellExecute = true,
                                WorkingDirectory = workDir
                            };

            try
            {
                _process = Process.Start(startInfo);
            }
            catch (Exception e)
            {
                string msg = $"Failed to start SSI process: {e.Message}";
                _log.Error(e, msg);
                throw new ListenerException(msg, e);
            }

            _timer = new Timer(CheckSsi, null, 1000, 1000);
        }

        private void InitializeSsi()
        {
            string zipPath = GetFilePath("vadnet.zip");
            if (!File.Exists(zipPath))
            {
                throw new ListenerException("vadnet.zip wasn't found!");
            }

            try
            {
                string curDir = GetCurrentDirectory();

                _log.Info($"Extracting {zipPath} to {curDir}");

                ZipFile.ExtractToDirectory(zipPath, curDir);
            }
            catch (Exception e)
            {
                _log.Error(e, "Error extracting vadnet!");
                throw new ListenerException("Error extracting vadnet!", e);
            }

            string workingDir = GetCurrentDirectory();
            workingDir = workingDir == string.Empty
                             ? "vadnet"
                             : Path.Combine(workingDir, "vadnet");

            var startInfo = new ProcessStartInfo("cmd", "/C do_bin.cmd")
                            {
                                UseShellExecute = false,
                                CreateNoWindow = false,
                                WorkingDirectory = workingDir,
                                RedirectStandardError = true,
                                RedirectStandardOutput = true
                            };

            _log.Info("Strating do_bin.cmd");

            var outBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            try
            {
                var process = Process.Start(startInfo);
                process.OutputDataReceived += (sender, args) => outBuilder.AppendLine(args.Data);
                process.ErrorDataReceived += (sender, args) => errorBuilder.AppendLine(args.Data);

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    string msg = $"do_bin.cmd exited with code {process.ExitCode}.\nstdout:\n{outBuilder.ToString()}\nstderr:\n{errorBuilder.ToString()}";
                    _log.Error(msg);
                    throw new ListenerException(msg);
                }

                _log.Info($"Finished running do_bin.cmd.\nstdout:\n{outBuilder.ToString()}\nstderr:\n{errorBuilder.ToString()}");
            }
            catch (ListenerException)
            {
                throw;
            }
            catch (Exception e)
            {
                string msg = $"Error running do_bin.cmd: {e.Message}.\nstdout:\n{outBuilder.ToString()}\nstderr:\n{errorBuilder.ToString()}";
                _log.Error(e, msg);
                throw new ListenerException(msg, e);
            }

            _log.Info("SSI extracted and initilized.");
        }

        private string GetFilePath(string path)
        {
            string location = GetCurrentDirectory();

            location = string.IsNullOrEmpty(location)
                           ? path
                           : Path.Combine(location, path);

            return location;
        }

        private string GetCurrentDirectory()
        {
            string location = Assembly.GetExecutingAssembly().Location;
            if (string.IsNullOrEmpty(location))
            {
                location = Environment.CurrentDirectory;
            }
            else
            {
                location = Path.GetDirectoryName(location);
            }

            return location ?? string.Empty;
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

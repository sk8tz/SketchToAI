using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SketchToAI
{
    public sealed class CliServerHost : IDisposable
    {
        private AsyncChannel<(string Query, TaskCompletionSource<string> Response)> _queue = 
            new AsyncChannel<(string Query, TaskCompletionSource<string> Response)>(16);
        private Task _hostTask { get; set; }

        public string Command { get; }
        public string Arguments { get; }
        public string WorkingDirectory { get; set; } = null;
        public TimeSpan AutoRestartDelay { get; set; } = TimeSpan.FromSeconds(1);
        public CancellationToken CancellationToken { get; set; }
        public bool IsRunning => _hostTask != null && !_hostTask.IsCompleted;
        public bool IsRestarting { get; private set; } 

        public CliServerHost(string command, string arguments = "")
        {
            Command = command;
            Arguments = arguments;
        }

        public void Dispose()
        {
            _queue.CompletePut();
        }

        public CliServerHost Start()
        {
            if (IsRunning)
                throw Errors.AlreadyRunning(GetType().Name);
            _hostTask = Task.Run(Run, CancellationToken);
            return this;
        }

        public async Task<string> Query(string query)
        {
            if (_hostTask == null || _hostTask.IsCompleted)
                throw Errors.NotRunning(GetType().Name);
            var tcs = new TaskCompletionSource<string>();
            await _queue.PutAsync((query, tcs), CancellationToken).ConfigureAwait(false);
            return await tcs.Task.ConfigureAwait(false);
        }

        private async Task Run()
        {
            do {
                try {
                    await RunOnce().ConfigureAwait(false);
                }
                catch (Exception e) {
                    Console.Error.WriteLine($"CLI Server failed: {e.Message}");
                }
                try {
                    IsRestarting = true;
                    await Task.Delay(AutoRestartDelay, CancellationToken).ConfigureAwait(false);
                }
                finally {
                    IsRestarting = false;
                }
            } while(AutoRestartDelay != TimeSpan.Zero);
        }

        private async Task RunOnce()
        {
            CancellationToken.ThrowIfCancellationRequested();
            var processStartInfo = new ProcessStartInfo(Command, Arguments) {
                UseShellExecute = false,
                ErrorDialog = false,
                WorkingDirectory = WorkingDirectory,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            using var process = new Process {StartInfo = processStartInfo};
            if (!process.Start())
                return;
            var inputWriter = process.StandardInput;
            var outputReader = process.StandardOutput;
            while (true) {
                var (entry, isDequeued) = await _queue.PullAsync(CancellationToken).ConfigureAwait(false);
                if (!isDequeued)
                    break;
                var query = entry.Query;
                var response = entry.Response;
                var queryBuffer = new ReadOnlyMemory<char>(query.ToArray());
                try {
                    await inputWriter.WriteLineAsync(queryBuffer, CancellationToken).ConfigureAwait(false);
                    // ReadLineAsync doesn't support cancellation, so
                    // we're trying to take care of that differently
                    var readLineTask = outputReader.ReadLineAsync();
                    await Task.WhenAny(readLineTask, CancellationToken.AsTask(false)).ConfigureAwait(false); 
                    CancellationToken.ThrowIfCancellationRequested();
                    response.SetResult(readLineTask.Result);
                }
                catch (Exception e) {
                    if (e is TaskCanceledException)
                        response.SetCanceled();
                    else
                        response.SetException(e);
                }
            }
        }
    }
}

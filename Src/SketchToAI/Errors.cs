using System;

namespace SketchToAI
{
    public static class Errors
    {
        public static Exception QueueSizeMustBeGreaterThanZero(string paramName) =>
            new ArgumentOutOfRangeException(paramName, "Queue size must be > 0.");
        public static Exception BufferLengthMustBeGreaterThanOne(string paramName) =>
            new ArgumentOutOfRangeException(paramName, "Buffer length must be > 1.");
        public static Exception BufferLengthMustBeGreaterThanZero(string paramName) =>
            new ArgumentOutOfRangeException(paramName, "Buffer length must be > 0.");
        public static Exception EnqueueCompleted() =>
            new InvalidOperationException("EnqueueCompleted == true.");

        public static Exception AlreadyInitialized() =>
            new InvalidOperationException("Already initialized.");
        public static Exception ObjectDisposed() =>
            new ObjectDisposedException(null, "The object is already disposed.");

        public static Exception AlreadyRunning(string name) =>
            new InvalidOperationException($"{name} is already running.");
        public static Exception NotRunning(string name) =>
            new InvalidOperationException($"{name} is not running.");
    }
}

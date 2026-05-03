using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace EternalAfternoonHeadTracking
{
    /// <summary>
    /// Static loader called from patched Assembly-CSharp.dll.
    /// Creates the HeadTracking mod GameObject on first call.
    /// Auto-recreates if destroyed.
    /// </summary>
    public static class ModLoader
    {
        private static bool _initialized;
        private static string _logPath;
        private static bool _needsRecreate;
        private static ModRecreator _recreator;

        // Log buffering for performance - reduces file I/O
        private static readonly StringBuilder _logBuffer = new StringBuilder(4096);
        private static int _logCount;
        private const int LogFlushThreshold = 10;

        /// <summary>
        /// Called from patched Assembly-CSharp.dll entry point.
        /// Called every frame from Update() - must be fast when already initialized.
        /// </summary>
        public static void Initialize()
        {
            // Fast path: already initialized and mod exists - no work needed
            if (_initialized && HeadTrackingMod.Instance != null) return;

            // Slow path: first init or recreation needed
            bool isRecreate = _initialized;
            if (!_initialized)
            {
                SetupLogging();
            }
            Log(isRecreate ? "ModLoader.Initialize() - recreating destroyed mod" : "ModLoader.Initialize() called");

            _initialized = true;

            // Create the mod GameObject with protection against destruction
            var modObject = new GameObject("HeadTracking");
            modObject.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(modObject);
            modObject.AddComponent<HeadTrackingMod>();

            // Create recreator helper if needed
            if (_recreator == null)
            {
                var recreatorObj = new GameObject("HeadTrackingRecreator");
                recreatorObj.hideFlags = HideFlags.HideAndDontSave;
                UnityEngine.Object.DontDestroyOnLoad(recreatorObj);
                _recreator = recreatorObj.AddComponent<ModRecreator>();
            }

            Log("HeadTracking mod initialized successfully!");
            FlushLog();
        }

        /// <summary>
        /// Schedule recreation of the mod on next frame.
        /// </summary>
        public static void ScheduleRecreate()
        {
            _needsRecreate = true;
        }

        /// <summary>
        /// Check if recreation is needed and perform it.
        /// </summary>
        internal static void CheckRecreate()
        {
            if (_needsRecreate && HeadTrackingMod.Instance == null)
            {
                _needsRecreate = false;
                Initialize();
            }
        }

        /// <summary>
        /// Reset the recreator reference so it can be recreated.
        /// </summary>
        internal static void ResetRecreator()
        {
            _recreator = null;
        }

        private static void SetupLogging()
        {
            string assemblyDir = Path.GetDirectoryName(typeof(ModLoader).Assembly.Location);
            if (assemblyDir == null)
            {
                throw new InvalidOperationException("Failed to determine assembly directory for logging. Assembly.Location returned null or invalid path.");
            }

            _logPath = Path.Combine(assemblyDir, "HeadTracking.log");

            if (File.Exists(_logPath))
            {
                File.Delete(_logPath);
            }
        }

        internal static void Log(string message)
        {
            if (string.IsNullOrEmpty(_logPath))
            {
                throw new InvalidOperationException("Cannot log: _logPath not initialized. Call SetupLogging() first.");
            }

            DateTime now = DateTime.Now;
            _logBuffer.Append('[');
            AppendTwoDigit(_logBuffer, now.Hour);
            _logBuffer.Append(':');
            AppendTwoDigit(_logBuffer, now.Minute);
            _logBuffer.Append(':');
            AppendTwoDigit(_logBuffer, now.Second);
            _logBuffer.Append('.');
            AppendThreeDigit(_logBuffer, now.Millisecond);
            _logBuffer.Append("] ").Append(message).Append('\n');
            _logCount++;

            if (_logCount >= LogFlushThreshold)
            {
                FlushLog();
            }
        }

        private static void AppendTwoDigit(StringBuilder sb, int value)
        {
            sb.Append((char)('0' + value / 10));
            sb.Append((char)('0' + value % 10));
        }

        private static void AppendThreeDigit(StringBuilder sb, int value)
        {
            sb.Append((char)('0' + value / 100));
            sb.Append((char)('0' + (value / 10) % 10));
            sb.Append((char)('0' + value % 10));
        }

        /// <summary>
        /// Flushes buffered log messages to disk.
        /// </summary>
        internal static void FlushLog()
        {
            if (_logBuffer.Length == 0) return;

            File.AppendAllText(_logPath, _logBuffer.ToString());
            _logBuffer.Length = 0;
            _logCount = 0;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace GanglandUndercover.Core
{
    /// <summary>
    /// M10.2 日志与崩溃上报模块。
    /// 轻量运行日志 + 崩溃捕获 → 本地文件持久化，封测期间收集反馈用。
    /// 挂载到 PrototypeBootstrap 或场景常驻 GameObject 上。
    /// </summary>
    public sealed class LogReporter : MonoBehaviour
    {
        // ─── 单例 ───────────────────────────────────────
        public static LogReporter Instance { get; private set; }

        // ─── 配置 ───────────────────────────────────────
        [Header("Persistence")]
        [SerializeField] private int maxLogFiles = 10;     // 最多保留日志文件数
        [SerializeField] private int maxLinesPerFile = 5000; // 单个文件最多行数

        // ─── 运行时 ───────────────────────────────────────
        private readonly Queue<string> _buffer = new Queue<string>();
        private string _logDir;
        private string _currentFilePath;
        private int _currentLineCount;
        private bool _initialized;

        // ─── Unity 生命周期 ───────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }

        private void OnEnable()
        {
            Application.logMessageReceived += OnLogMessageReceived;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= OnLogMessageReceived;
        }

        private void OnDestroy()
        {
            Flush();
        }

        // ─── 初始化 ───────────────────────────────────────

        private void Initialize()
        {
            _logDir = Path.Combine(Application.persistentDataPath, "logs");
            Directory.CreateDirectory(_logDir);

            PurgeOldLogs();
            CreateNewLogFile();

            _initialized = true;

            Info("LogReporter", $"Logging started. Unity {Application.unityVersion}, platform {Application.platform}, v{Application.version}");
            Info("LogReporter", $"Log directory: {_logDir}");
        }

        private void CreateNewLogFile()
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _currentFilePath = Path.Combine(_logDir, $"game_{timestamp}.log");
            _currentLineCount = 0;
        }

        private void PurgeOldLogs()
        {
            try
            {
                string[] files = Directory.GetFiles(_logDir, "game_*.log");
                Array.Sort(files);
                if (files.Length >= maxLogFiles)
                {
                    for (int i = 0; i < files.Length - maxLogFiles + 1; i++)
                    {
                        File.Delete(files[i]);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LogReporter] Failed to purge old logs: {e.Message}");
            }
        }

        // ─── 日志写入 ───────────────────────────────────────

        private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            if (!_initialized) return;

            string entry = FormatLogEntry(condition, stackTrace, type);
            _buffer.Enqueue(entry);
        }

        private string FormatLogEntry(string condition, string stackTrace, LogType type)
        {
            string tag = type switch
            {
                LogType.Error   => "E",
                LogType.Assert  => "A",
                LogType.Warning => "W",
                LogType.Exception => "X",
                _               => "I",
            };

            var sb = new StringBuilder();
            sb.Append(DateTime.Now.ToString("HH:mm:ss.fff"));
            sb.Append(' ');
            sb.Append(tag);
            sb.Append(' ');
            sb.Append(condition);

            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                sb.Append('\n');
                sb.Append(stackTrace);
            }

            return sb.ToString();
        }

        // ─── 公共 API ───────────────────────────────────────

        /// <summary>写入一条 Info 级别日志</summary>
        public static void Info(string tag, string message)
        {
            var sb = new StringBuilder();
            sb.Append(DateTime.Now.ToString("HH:mm:ss.fff"));
            sb.Append(" I ");
            sb.Append('[');
            sb.Append(tag);
            sb.Append("] ");
            sb.Append(message);

            string entry = sb.ToString();
            Instance?._buffer.Enqueue(entry);
            Debug.Log($"[{tag}] {message}");
        }

        /// <summary>写入一条 Warning 级别日志</summary>
        public static void Warn(string tag, string message)
        {
            var sb = new StringBuilder();
            sb.Append(DateTime.Now.ToString("HH:mm:ss.fff"));
            sb.Append(" W ");
            sb.Append('[');
            sb.Append(tag);
            sb.Append("] ");
            sb.Append(message);

            string entry = sb.ToString();
            Instance?._buffer.Enqueue(entry);
            Debug.LogWarning($"[{tag}] {message}");
        }

        /// <summary>获取日志目录路径</summary>
        public static string LogDirectory => Instance?._logDir ?? Path.Combine(Application.persistentDataPath, "logs");

        // ─── 持久化 ───────────────────────────────────────

        private void LateUpdate()
        {
            FlushIfNeeded();
        }

        private void FlushIfNeeded()
        {
            if (_buffer.Count > 50 || _currentLineCount > maxLinesPerFile)
            {
                Flush();
            }
        }

        public void Flush()
        {
            if (_buffer.Count == 0) return;
            if (string.IsNullOrEmpty(_currentFilePath)) return;

            try
            {
                if (_currentLineCount >= maxLinesPerFile)
                {
                    CreateNewLogFile();
                }

                using var writer = new StreamWriter(_currentFilePath, append: true, Encoding.UTF8);
                while (_buffer.Count > 0 && _currentLineCount < maxLinesPerFile)
                {
                    writer.WriteLine(_buffer.Dequeue());
                    _currentLineCount++;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LogReporter] Flush failed: {e.Message}");
            }
        }
    }
}

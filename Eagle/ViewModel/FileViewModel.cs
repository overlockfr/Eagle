﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using GalaSoft.MvvmLight;
using System.Threading;
using System.Text;

namespace Eagle.ViewModel
{
    public class FileViewModel : ViewModelBase
    {
        private bool _currentLineUnfinished;
        private Encoding _encoding = Encoding.Default;
        private string _fileName;
        private int _currentReadPosition = 0;
        private int _currentLineNumber = 1;
        private int _currentLineIndex = 0;
        private FileStream _fileStream;
        private FileSystemWatcher _fileWatcher;
        private ObservableCollection<LineViewModel> _lines = new ObservableCollection<LineViewModel>();
        private byte _previousReadLastByte = 0;
        private SynchronizationContext _syncContext;

        public ObservableCollection<LineViewModel> Lines
        {
            get
            {
                return _lines;
            }
            set
            {
                if (_lines != value)
                {
                    _lines = value;
                    this.RaisePropertyChanged("Lines");
                }
            }
        }

        public FileViewModel(string fileName, Encoding encoding = null)
        {
            _encoding = encoding ?? Encoding.Default;

            this.Lines = new ObservableCollection<LineViewModel>();
            this.FileName = fileName;
            _syncContext = SynchronizationContext.Current;

            if (IsInDesignMode)
            {
                this.Lines.Add(new LineViewModel("Hello, World!"));
                this.Lines.Add(new LineViewModel("Hello, World!"));
                this.Lines.Add(new LineViewModel("Hello, World!"));
                this.Lines.Add(new LineViewModel("Hello, World!"));
                this.Lines.Add(new LineViewModel("Hello, World!"));
                this.FileName = @"C:\Users\Nicolas\Desktop\Test.txt";
            }
            else
            {
                this.Lines.Add(new LineViewModel("Press Open to open a new file"));

            }
        }

        public string FileName
        {
            get
            {
                return _fileName;
            }

            set
            {
                if (_fileName != value)
                {
                    var old = _fileName;
                    _fileName = value;
                    this.RaisePropertyChanged("FileName", old, value, true);
                }
            }
        }

        public void LoadFileContent()
        {
            this.Lines.Clear();
            try
            {
                InitializeReadParameters();
                //var sw = Stopwatch.StartNew();
                var lines = new ObservableCollection<LineViewModel>(ReadLines());
                //MessageBox.Show(string.Format("{0} lines read in {1} ms", lines.Count, sw.ElapsedMilliseconds));
                this.Lines = lines;
            }
            catch (Exception ex)
            {
                this.Lines.Clear();
                this.Lines.Add(new LineViewModel(string.Format("Error opening file: {0}", ex)));
            }

            // Setup File change watcher
            _fileWatcher = new FileSystemWatcher(Path.GetDirectoryName(_fileName), Path.GetFileName(_fileName))
                                    {
                                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.Attributes
                                    };
            _fileWatcher.Changed += OnFileChanged;
            _fileWatcher.EnableRaisingEvents = true;
        }

        private void InitializeReadParameters()
        {
            _currentLineNumber = 1;
            _currentLineIndex = 0;
            _currentReadPosition = 0;
            _previousReadLastByte = 0;
            _currentLineUnfinished = false;
        }

        private IEnumerable<LineViewModel> ReadLines()
        {
            _fileStream = File.Open(_fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            _fileStream.Seek(_currentReadPosition, SeekOrigin.Begin);
            var buffer = new byte[20480];
            int read = 0;
            while (true)
            {
                read = _fileStream.Read(buffer, 0, buffer.Length);
                if (read == 0)
                    break;

                for (int i = 0; i < read; i++, _currentReadPosition++)
                {
                    if (buffer[i] == '\n')
                    {
                        var lastLineCharacter = (i > 0) ? buffer[i - 1] : _previousReadLastByte;
                        LineViewModel newLine = ReadNewLine(lastLineCharacter, false);
                        if (newLine != null)
                            yield return newLine;
                    }

                }

                if (read > 0) _previousReadLastByte = buffer[read - 1];
            }

            // End-of-file reached; if the last character read wasn't '\n', add the unfinished line
            if (_previousReadLastByte != '\n' && _previousReadLastByte != '\0')
            {
                var line = ReadNewLine(_previousReadLastByte, true);
                if (line != null)
                    yield return line;
            }

            using (_fileStream) _fileStream = null;
        }

        private LineViewModel ReadNewLine(byte lastLineCharacter, bool isLineUnfinished)
        {
            int length = _currentReadPosition - _currentLineIndex;
            if (lastLineCharacter == '\r')
            {
                length--;
            }

            // New line reached
            LineViewModel newLine = null;
            // Check if an unfinished line was being read
            if (_currentLineUnfinished)
            {
                // Just update the current line with a new length
                var lastLine = this.Lines[this.Lines.Count - 1];
                lastLine.Length = length;
            }
            else
            {
                newLine = new LineViewModel(_currentLineNumber++, _currentLineIndex, length, _fileName, _encoding);
            }

            if (isLineUnfinished)
            {
                _currentLineUnfinished = true;
            }
            else
            {
                _currentLineIndex = _currentReadPosition + 1;
                _currentLineUnfinished = false;
            }

            return newLine;
        }

        void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            switch (e.ChangeType)
            {
                case WatcherChangeTypes.Created:
                    break;
                case WatcherChangeTypes.Deleted:
                    break;
                case WatcherChangeTypes.Changed:
                    var lines = ReadLines().ToList();

                    _syncContext.Post(_ =>
                        {
                            foreach (var line in lines)
                            {
                                this.Lines.Add(line);
                            }
                        }, null);
                    break;
                case WatcherChangeTypes.Renamed:
                    break;
                case WatcherChangeTypes.All:
                    break;
            }
        }

        public override void Cleanup()
        {
            base.Cleanup();

            using (_fileStream)
            using (_fileWatcher)
            {
                _fileStream = null;
                _fileWatcher = null;
            }
        }
    }
}

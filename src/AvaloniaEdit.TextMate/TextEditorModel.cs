using System;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using TextMateSharp.Model;

namespace AvaloniaEdit.TextMate
{
    class TextEditorModel : AbstractLineList, IModelTokensChangedListener
    {
        private object _lock = new object();
        private readonly TextDocument _document;
        private readonly TextEditor _editor;
        private int _lineCount;

        public TextEditorModel(TextEditor editor, TextDocument document)
        {
            _editor = editor;
            _document = document;
            
            _lineCount = _document.LineCount;
            
            _document.Changing +=  DocumentOnChanging;
            _document.Changed += DocumentOnChanged;
            _document.LineCountChanged += DocumentOnLineCountChanged;
            
            for (int i = 0; i < _document.LineCount; i++)
            {
                AddLine(i);
            }   
        }
        
        private void DocumentOnLineCountChanged(object? sender, EventArgs e)
        {
            lock (_lock)
            {
                _lineCount = _document.LineCount;
            }
        }

        private void DocumentOnChanging(object? sender, DocumentChangeEventArgs e)
        {
            if (e.RemovedText is { })
            {
                var startLine = _document.GetLineByOffset(e.Offset).LineNumber - 1;
                var endLine = _document.GetLineByOffset(e.Offset + e.RemovalLength).LineNumber - 1;
                
                for (int i = endLine; i > startLine; i--) 
                {
                    RemoveLine(i);
                }
            }
        }

        private void DocumentOnChanged(object? sender, DocumentChangeEventArgs e)
        {
            int startLine = _document.GetLineByOffset(e.Offset).LineNumber - 1;

            if (e.InsertedText is { })
            {
                int endLine = _document.GetLineByOffset(e.Offset + e.InsertionLength).LineNumber - 1;

                for (int i = startLine; i < endLine; i++)
                {
                    AddLine(i);
                }

                if (startLine == endLine)
                {
                    UpdateLine(startLine);
                }
            }
            else
            {
                UpdateLine(startLine);
            }
            
            InvalidateLine(startLine);
        }

        public override void UpdateLine(int lineIndex)
        {
            // No op
        }

        public override int GetNumberOfLines() => _lineCount;

        public override string GetLineText(int lineIndex)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                return _document.GetText(_document.Lines[lineIndex]);
            }

            return Dispatcher.UIThread.InvokeAsync(() => { return _document.GetText(_document.Lines[lineIndex]); })
                .GetAwaiter().GetResult();
        }

        public override int GetLineLength(int lineIndex)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                return _document.Lines[lineIndex].Length;
            }
            
            return Dispatcher.UIThread.InvokeAsync(() =>
            {
                return _document.Lines[lineIndex].Length;
            }).GetAwaiter().GetResult();
        }

        public override void Dispose()
        {
            // todo implement dispose.
        }

        public void ModelTokensChanged(ModelTokensChangedEvent e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    foreach (var range in e.ranges)
                    {
                        var startLine = _document.GetLineByNumber(range.fromLineNumber);
                        var endLine = _document.GetLineByNumber(range.toLineNumber);

                        _editor.TextArea.TextView.Redraw(startLine.Offset, endLine.EndOffset - startLine.Offset);
                    }
                }
                catch (Exception e)
                {
                    _editor.TextArea.TextView.Redraw();
                }
            });
        }
    }
}
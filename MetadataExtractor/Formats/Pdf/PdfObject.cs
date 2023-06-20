// Copyright (c) Drew Noakes and contributors. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;

namespace MetadataExtractor.Formats.Pdf
{
    internal interface PdfObject
    {
        string Type { get; }

        object? GetValue();

        void Add(object? value);
    }

    internal class PdfRoot : PdfObject
    {
        private object? _value;

        private bool _valueWasSet;

        public virtual string Type => "root";

        public PdfRoot()
        {
            _valueWasSet = false;
        }

        public object? GetValue()
        {
            if (!_valueWasSet)
            {
                throw new Exception("Value was not set");
            }
            return _value;
        }

        public void Add(object? value)
        {
            if (_valueWasSet)
            {
                throw new Exception("Value already set");
            }
            _value = value;
            _valueWasSet = true;
        }
    }

    internal class PdfIndirectObject : PdfRoot
    {
        public override string Type => "indirect-object";
    }

    internal class PdfArray : PdfObject
    {
        private readonly List<object?> _array;

        public string Type => "array";

        public PdfArray()
        {
            _array = new List<object?>();
        }

        public object? GetValue()
        {
            return _array;
        }

        public void Add(object? value)
        {
            _array.Add(value);
        }
    }

    internal class PdfDictionary : PdfObject
    {
        private readonly Dictionary<string, object?> _dictionary;

        private string? _currentKey = null;

        public string Type => "dictionary";

        public PdfDictionary()
        {
            _dictionary = new Dictionary<string, object?>();
        }

        public object? GetValue()
        {
            return _dictionary;
        }

        public void Add(object? value)
        {
            if (value is CommentToken || value is DummyToken)
            {
                return;
            }

            if (value is NameToken)
            {
                if (_currentKey is not null)
                {
                    if (value is not NullToken)
                    {
                        _dictionary.Add(_currentKey, value);
                    }
                    _currentKey = null;
                }
                else
                {
                    _currentKey = Encoding.ASCII.GetString((value as NameToken)!.Value); // TODO dictionary keys are probably ASCII
                }
            }
            else
            {
                if (_currentKey is null)
                {
                    return;
                }
                if (value is not NullToken)
                {
                    _dictionary.Add(_currentKey, value);
                }
                _currentKey = null;
            }
        }
    }
}

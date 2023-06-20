// Copyright (c) Drew Noakes and contributors. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;

namespace MetadataExtractor.Formats.Pdf
{
    internal abstract class PdfObject
    {
        public abstract string Type { get; }

        public abstract bool HasValue { get; }

        public abstract object? GetValue();

        public void Add(PdfObject value)
        {
            AddValue(value);
        }

        public void Add(Token token)
        {
            if (token is DummyToken)
            {
                return;
            }
            else if (token is CommentToken)
            {
                return;
            }
            else if (token is NullToken)
            {
                AddValue(null);
            }
            else if (token is BooleanToken booleanToken)
            {
                AddValue(booleanToken.BooleanValue);
            }
            else if (token is NumericIntegerToken numericIntegerToken)
            {
                AddValue(numericIntegerToken.IntegerValue);
            }
            else if (token is NumericRealToken numericRealToken)
            {
                AddValue(numericRealToken.RealValue);
            }
            else if (token is StringToken stringToken)
            {
                AddValue(stringToken.StringValue); // TODO encoding is context-specific
            }
            else if (token is NameToken nameToken)
            {
                AddValue(nameToken.StringValue);
            }
            else
            {
                throw new Exception($"Unexpected token type {token.Type}");
            }
        }

        protected abstract void AddValue(object? value);
    }

    internal class PdfRoot : PdfObject
    {
        private object? _value;

        private bool _valueWasSet;

        public override string Type => "root";

        public override bool HasValue => _valueWasSet;

        public PdfRoot()
        {
            _valueWasSet = false;
        }

        public override object? GetValue()
        {
            if (!_valueWasSet)
            {
                throw new Exception("Value was not set");
            }
            return _value;
        }

        protected override void AddValue(object? value)
        {
            _value = value;
            _valueWasSet = true;
        }
    }

    internal class PdfIndirectReference : PdfObject
    {
        /// <summary>
        /// Gets the sequential object number.
        /// </summary>
        public uint ObjectNumber { get; }

        /// <summary>
        /// The 5-digit generation number. The maximum generation number is 65,535.
        /// </summary>
        public ushort Generation { get; }

        public override string Type => "indirect-reference";

        public override bool HasValue => true;

        public PdfIndirectReference(uint objectNumber, ushort generation)
        {
            ObjectNumber = objectNumber;
            Generation = generation;
        }

        public PdfIndirectReference(int objectNumber, int generation)
        {
            if (objectNumber < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(objectNumber));
            }
            if (generation < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(generation));
            }
            if (generation > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(generation));
            }
            ObjectNumber = (uint)objectNumber;
            Generation = (ushort)generation;
        }

        public override object? GetValue()
        {
            // returns the cache key of the object
            return $"{ObjectNumber}-{Generation}";
        }

        protected override void AddValue(object? value)
        {
            throw new NotImplementedException();
        }
    }

    internal class PdfIndirectObject : PdfObject
    {
        private object? _value;

        private bool _valueWasSet;

        public override string Type => "indirect-object";

        public override bool HasValue => _valueWasSet;

        public PdfIndirectObject()
        {
            _valueWasSet = false;
        }

        public override object? GetValue()
        {
            if (!_valueWasSet)
            {
                throw new Exception("Value was not set");
            }
            return _value;
        }

        protected override void AddValue(object? value)
        {
            if (_valueWasSet)
            {
                throw new Exception("Value already set");
            }
            _value = value;
            _valueWasSet = true;
        }
    }

    internal class PdfArray : PdfObject
    {
        private readonly List<object?> _array;

        public override string Type => "array";

        public override bool HasValue => true;

        public PdfArray()
        {
            _array = new List<object?>();
        }

        public override object? GetValue()
        {
            return _array;
        }

        protected override void AddValue(object? value)
        {
            _array.Add(value);
        }
    }

    internal class PdfDictionary : PdfObject
    {
        private readonly Dictionary<string, object?> _dictionary;

        private string? _currentKey = null;

        public override string Type => "dictionary";

        public override bool HasValue => true;

        public PdfDictionary()
        {
            _dictionary = new Dictionary<string, object?>();
        }

        public override object? GetValue()
        {
            return _dictionary;
        }

        protected override void AddValue(object? value)
        {
            if (value is NameToken nameToken)
            {
                if (_currentKey is not null)
                {
                    _dictionary.Add(_currentKey, value);
                    _currentKey = null;
                }
                else
                {
                    _currentKey = Encoding.ASCII.GetString(nameToken.Value); // documented dictionary keys are probably ASCII
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

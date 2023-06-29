// Copyright (c) Drew Noakes and contributors. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;

namespace MetadataExtractor.Formats.Pdf
{
    public abstract class PdfObject
    {
    }

    public interface IPdfContainer
    {
        public abstract string Type { get; }

        void Nest<T>(PdfContainer<T> pdfContainer);

        void Add(PdfObject pdfObject);
    }

    public abstract class PdfContainer<T> : PdfObject, IPdfContainer
    {
        public abstract string Type { get; }

        public abstract T Value { get; }

        public void Nest<U>(PdfContainer<U> pdfContainer)
        {
            Add(pdfContainer);
        }

        public abstract void Add(PdfObject pdfObject);
    }

    public abstract class PdfScalarObject<T> : PdfObject
    {
        public abstract T Value { get; }
    }

    #region Scalar objects

    public class PdfNull : PdfObject
    {
    }

    public class PdfBoolean : PdfScalarObject<bool>
    {
        public override bool Value { get; }

        public PdfBoolean(bool value)
        {
            Value = value;
        }
    }

    public class PdfNumericInteger : PdfScalarObject<int>
    {
        public override int Value { get; }

        public PdfNumericInteger(int value)
        {
            Value = value;
        }
    }

    public class PdfNumericReal : PdfScalarObject<decimal>
    {
        public override decimal Value { get; }

        public PdfNumericReal(decimal value)
        {
            Value = value;
        }
    }

    public class PdfString : PdfScalarObject<StringValue>
    {
        public override StringValue Value { get; }

        public PdfString(StringValue value)
        {
            Value = value;
        }

        public string ToASCIIString()
        {
            return Value.ToString(Encoding.ASCII);
        }

        public string ToUTF8String()
        {
            return Value.ToString(Encoding.UTF8);
        }
    }

    public class PdfName : PdfString
    {
        public PdfName(StringValue value)
            : base(value)
        {
        }
    }

    public class PdfIndirectReference : PdfScalarObject<ObjectIdentifier>
    {
        public override ObjectIdentifier Value { get; }

        public PdfIndirectReference(ObjectIdentifier value)
        {
            Value = value;
        }

        public PdfIndirectReference(uint objectNumber, ushort generationNumber)
           : base()
        {
            Value = new ObjectIdentifier(objectNumber, generationNumber);
        }

        public PdfIndirectReference(int objectNumber, int generationNumber)
            : base()
        {
            Value = new ObjectIdentifier(objectNumber, generationNumber);
        }
    }

    public class PdfStream : PdfObject
    {
        // from the spec:
        // All streams shall be indirect objects and the stream dictionary shall be a direct object

        public ObjectIdentifier Identifier { get; }

        public PdfDictionary StreamDictionary { get; }

        public int StreamStartIndex { get; }

        public PdfStream(ObjectIdentifier identifier, PdfDictionary streamDictionary, int streamStartIndex)
        {
            Identifier = identifier;

            StreamDictionary = streamDictionary;

            StreamStartIndex = streamStartIndex;
        }
    }

    #endregion Scalar objects

    #region Containers

    public class PdfRoot : PdfContainer<PdfObject>
    {
        private PdfObject? _value;

        public override PdfObject Value
        {
            get
            {
                if (_value is null)
                {
                    throw new Exception("Value was not set"); // TODO: getters are not supposed to throw
                }
                return _value;
            }
        }

        public override string Type => "root";

        public ObjectIdentifier Identifier { get; }

        public PdfRoot()
        {
            _value = null;
        }

        public PdfRoot(PdfObject? value)
        {
            _value = value;
        }

        public override void Add(PdfObject pdfObject)
        {
            if (_value is not null)
            {
                throw new Exception("Value already set");
            }
            _value = pdfObject;
        }

        public void ReplaceValue(PdfObject pdfObject)
        {
            _value = pdfObject;
        }
    }

    public class PdfIndirectObject : PdfRoot
    {
        public override string Type => "indirect-object";

        public ObjectIdentifier Identifier { get; }

        public PdfIndirectObject(uint objectNumber, ushort generationNumber)
            : base()
        {
            Identifier = new ObjectIdentifier(objectNumber, generationNumber);
        }

        public PdfIndirectObject(int objectNumber, int generationNumber)
            : base()
        {
            Identifier = new ObjectIdentifier(objectNumber, generationNumber);
        }

        public PdfIndirectObject(int objectNumber, int generationNumber, PdfObject? value)
            : base(value)
        {
            Identifier = new ObjectIdentifier(objectNumber, generationNumber);
        }
    }

    public class PdfArray : PdfContainer<List<PdfObject>>
    {
        public override List<PdfObject> Value { get; }

        public override string Type => "array";

        public PdfArray()
        {
            Value = new List<PdfObject>();
        }

        public PdfArray(IEnumerable<PdfObject> values)
        {
            Value = new List<PdfObject>(values);
        }

        public override void Add(PdfObject pdfObject)
        {
            Value.Add(pdfObject);
        }
    }

    public class PdfDictionary : PdfContainer<Dictionary<string, PdfObject>>
    {
        public override Dictionary<string, PdfObject> Value { get; }

        private string? _currentKey = null;

        public override string Type => "dictionary";

        public PdfDictionary()
        {
            Value = new Dictionary<string, PdfObject>();
        }

        public PdfDictionary(IDictionary<string, PdfObject> values)
        {
            Value = new Dictionary<string, PdfObject>(values);
        }

        public override void Add(PdfObject pdfObject)
        {
            if (pdfObject is PdfName name)
            {
                if (_currentKey is not null)
                {
                    Value.Add(_currentKey, pdfObject); // names can be values, too, in which case their encoding is probably UTF8
                    _currentKey = null;
                }
                else
                {
                    _currentKey = name.ToASCIIString(); // documented dictionary keys are probably ASCII
                }
            }
            else
            {
                if (_currentKey is null)
                {
                    return;
                }
                if (pdfObject is not PdfNull)
                {
                    Value.Add(_currentKey, pdfObject);
                }
                _currentKey = null;
            }
        }

        public int GetNumericIntegerForKey(string key)
        {
            if (Value[key] is PdfNumericInteger numericInteger) return numericInteger.Value;
            else throw new Exception($"Value for key {key} is not a numeric integer");
        }
    }

    #endregion Containers
}

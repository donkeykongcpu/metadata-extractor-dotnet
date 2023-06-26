// Copyright (c) Drew Noakes and contributors. All Rights Reserved. Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;

namespace MetadataExtractor.Formats.Pdf
{
    internal class PdfParser
    {
        private class ParseContext
        {
            private readonly Stack<PdfObject> _stack;

            public bool IsRoot => _stack.Peek().Type == "root";

            public bool HasValue => _stack.Peek().HasValue;

            private string ContextType => _stack.Peek().Type;

            public ParseContext()
            {
                _stack = new Stack<PdfObject>();
                _stack.Push(new PdfRoot()); // make sure stack is never empty
            }

            public void Add(PdfObject pdfObject)
            {
                _stack.Peek().Add(pdfObject);
            }

            public void StartContext(string type)
            {
                PdfObject pdfObject;
                switch (type)
                {
                    case "root": throw new Exception("Cannot start root context");
                    case "indirect-object": pdfObject = new PdfIndirectObject(); break;
                    case "array": pdfObject = new PdfArray(); break;
                    case "dictionary": pdfObject = new PdfDictionary(); break;
                    default: throw new Exception("Invalid type");
                }
                Add(pdfObject);
                _stack.Push(pdfObject);
            }

            public void EndContext(string type)
            {
                if (ContextType != type)
                {
                    throw new Exception("Context type mismatch");
                }
                _stack.Pop();
                if (_stack.Count < 1)
                {
                    throw new Exception("Context underflow");
                }
            }

            public object? GetValue()
            {
                if (_stack.Count != 1)
                {
                    throw new Exception("Invalid context count");
                }
                return _stack.Peek().GetValue();
            }
        }

        public static object? ParseObject(string input, int startIndex)
        {
            StringByteProviderSource byteProviderSource = new StringByteProviderSource(input, startIndex, ExtractionDirection.Forward);

            BufferedItemProvider<byte> byteProvider = new BufferedItemProvider<byte>(byteProviderSource, 1024);

            ItemProvider<Token> tokenProvider = GetTokenProvider(byteProvider);

            return ParseObject(tokenProvider);
        }

        public static object? ParseObject(IndexedReader reader, int startIndex)
        {
            IndexedReaderByteProviderSource byteProviderSource = new IndexedReaderByteProviderSource(reader, startIndex, ExtractionDirection.Forward);

            BufferedItemProvider<byte> byteProvider = new BufferedItemProvider<byte>(byteProviderSource, 1024);

            ItemProvider<Token> tokenProvider = GetTokenProvider(byteProvider);

            return ParseObject(tokenProvider);
        }

        private static ItemProvider<Token> GetTokenProvider(ItemProvider<byte> byteProvider)
        {
            PdfTokeniser tokeniser = new PdfTokeniser(byteProvider);

            IEnumerable<Token> tokens = tokeniser.Tokenise();

            EnumeratedItemProviderSource<Token> tokenSource = new EnumeratedItemProviderSource<Token>(tokens, new DummyToken());

            BufferedItemProvider<Token> tokenProvider = new BufferedItemProvider<Token>(tokenSource, 5);

            return tokenProvider;
        }

        public static object? ParseObject(ItemProvider<Token> tokenProvider)
        {
            var parseContext = new ParseContext();

            while (tokenProvider.HasNextItem)
            {
                // first check if we have either an indirect object (objectNumber generation "obj" ... "endobj")
                // or an indirect reference to an object (objectNumber generation "R")

                if (tokenProvider.PeekNextItem(0) is NumericIntegerToken objectNumberToken
                    && tokenProvider.PeekNextItem(1) is NumericIntegerToken generationToken
                    )
                {
                    var objectNumber = objectNumberToken.IntegerValue;
                    var generation = generationToken.IntegerValue;

                    if (tokenProvider.PeekNextItem(2) is IndirectObjectBeginToken)
                    {
                        parseContext.StartContext("indirect-object");
                        tokenProvider.Consume(3);
                        continue;
                    }
                    else if (tokenProvider.PeekNextItem(2) is IndirectReferenceMarkerToken)
                    {
                        parseContext.Add(new PdfIndirectReference(objectNumber, generation));
                        tokenProvider.Consume(3);
                        continue;
                    }
                }

                var nextToken = tokenProvider.GetNextItem();

                if (nextToken is ArrayBeginToken)
                {
                    parseContext.StartContext("array");
                }
                else if (nextToken is ArrayEndToken)
                {
                    parseContext.EndContext("array");
                }
                else if (nextToken is DictionaryBeginToken)
                {
                    parseContext.StartContext("dictionary");
                }
                else if (nextToken is DictionaryEndToken)
                {
                    parseContext.EndContext("dictionary");
                }
                else if (nextToken is IndirectObjectEndToken)
                {
                    parseContext.EndContext("indirect-object");
                }
                else if (nextToken is CommentToken)
                {
                    continue; // ignore comments
                }
                else
                {
                    parseContext.Add(PdfScalarValue.FromToken(nextToken));
                }

                if (parseContext.IsRoot)
                {
                    break;
                }

              


                //if (nextToken == "stream")
                //{
                //    // TODO implement stream objects

                //    break;
                //}

                //if (nextToken == "xref")
                //{
                //    // TODO implement cross-reference table objects

                //    break;
                //}

                //if (nextToken == "trailer")
                //{
                //    // TODO implement trailer objects

                //    break;
                //}


            }

            return parseContext.GetValue();
        }



    }
}

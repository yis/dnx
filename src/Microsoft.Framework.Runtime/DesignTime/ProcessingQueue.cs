﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Newtonsoft.Json;

namespace Microsoft.Framework.Runtime
{
    internal class ProcessingQueue
    {
        private readonly BinaryReader _reader;
        private readonly BinaryWriter _writer;

        public event Action<int, CompileResponse> OnReceive;
        public event Action<Dictionary<string, int>> ProjectsInitialized;

        public ProcessingQueue(Stream stream)
        {
            _reader = new BinaryReader(stream);
            _writer = new BinaryWriter(stream);
        }

        public void Start()
        {
            new Thread(ReceiveMessages).Start();
        }

        public void Post(DesignTimeMessage message)
        {
            lock (_writer)
            {
                _writer.Write(JsonConvert.SerializeObject(message));
            }
        }

        private void ReceiveMessages()
        {
            try
            {
                while (true)
                {
                    var messageType = _reader.ReadString();

                    if (messageType == "GetCompiledAssembly")
                    {
                        var compileResponse = new CompileResponse();
                        var id = _reader.ReadInt32();
                        var warningsCount = _reader.ReadInt32();
                        compileResponse.Warnings = new string[warningsCount];
                        for (int i = 0; i < warningsCount; i++)
                        {
                            compileResponse.Warnings[i] = _reader.ReadString();
                        }

                        var errorsCount = _reader.ReadInt32();
                        compileResponse.Errors = new string[errorsCount];
                        for (int i = 0; i < errorsCount; i++)
                        {
                            compileResponse.Errors[i] = _reader.ReadString();
                        }
                        var embeddedReferencesCount = _reader.ReadInt32();
                        compileResponse.EmbeddedReferences = new Dictionary<string, byte[]>();
                        for (int i = 0; i < embeddedReferencesCount; i++)
                        {
                            var key = _reader.ReadString();
                            int valueLength = _reader.ReadInt32();
                            var value = _reader.ReadBytes(valueLength);
                            compileResponse.EmbeddedReferences[key] = value;
                        }

                        var assemblyBytesLength = _reader.ReadInt32();
                        compileResponse.AssemblyBytes = _reader.ReadBytes(assemblyBytesLength);
                        var pdbBytesLength = _reader.ReadInt32();
                        compileResponse.PdbBytes = _reader.ReadBytes(pdbBytesLength);

                        OnReceive(id, compileResponse);
                    }
                    else if (messageType == "EnumerateProjectContexts")
                    {
                        int count = _reader.ReadInt32();
                        var projectContexts = new Dictionary<string, int>();
                        for (int i = 0; i < count; i++)
                        {
                            string key = _reader.ReadString();
                            int id = _reader.ReadInt32();

                            projectContexts[key] = id;
                        }

                        ProjectsInitialized(projectContexts);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Entity.AzureTableStorage.Interfaces;
using Microsoft.Data.Entity.AzureTableStorage.Wrappers;
using Microsoft.WindowsAzure.Storage.Table;
using Moq;

namespace Microsoft.Data.Entity.AzureTableStorage.Tests
{
    public class FakeConnection : AzureTableStorageConnection
    {
        private Dictionary<string,Queue<ITableResult>> _queue = new Dictionary<string, Queue<ITableResult>>(); 
        public void QueueResult(string tableName,ITableResult nextResult)
        {
            _queue[tableName] = _queue.ContainsKey(tableName) ?_queue[tableName] : new Queue<ITableResult>();
            _queue[tableName].Enqueue(nextResult);
        }

        public override ICloudTable GetTableReference(string name)
        {
            _queue[name] = _queue.ContainsKey(name) ?_queue[name] : new Queue<ITableResult>();
            var mockTable = new Mock<ICloudTable>();
            mockTable.Setup(s => s.CreateIfNotExistsAsync(default(CancellationToken)))
                .Returns(() => Task.Factory.StartNew(() => CreateTableRequests++));
            mockTable.Setup(s => s.CreateIfNotExists())
                .Callback(() => CreateTableRequests++);
            mockTable.Setup(s => s.ExecuteAsync(It.IsAny<TableOperation>(),default(CancellationToken)))
                .Returns(() => Task.Factory.StartNew<ITableResult>(() => _queue[name].Dequeue()));
            return mockTable.Object;
        }

        public int CreateTableRequests { get; private set; }

        public void ClearQueue()
        {
            _queue = new Dictionary<string, Queue<ITableResult>>();
        }
    }
}
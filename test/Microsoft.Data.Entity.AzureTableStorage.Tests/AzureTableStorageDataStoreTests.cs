// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Data.Entity.ChangeTracking;
using Microsoft.Data.Entity.Infrastructure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Moq;
using Xunit;

namespace Microsoft.Data.Entity.AzureTableStorage.Tests
{

    using ResultTaskList = IList<TableResult>;

    public class AzureTableStorageDataStoreTests : AzureTableStorageDataStore
    {
        private readonly TableResult _good;
        private readonly TableResult _bad;
        public AzureTableStorageDataStoreTests()
        {
            _good = new TableResult { HttpStatusCode = (int)HttpStatusCode.OK };
            _bad = new TableResult { HttpStatusCode = (int)HttpStatusCode.BadRequest };
        }
        private Task<TResult>[] SetupResults<TResult>(IEnumerable<TResult> tableResults)
        {
            var batch = new List<Task<TResult>>();
            foreach (var tableResult in tableResults)
            {
                var taskSource = new TaskCompletionSource<TResult>();
                taskSource.SetResult(tableResult);
                batch.Add(taskSource.Task);
            }
            return batch.ToArray();
        }

        [Fact]
        public void It_counts_batch_results()
        {
            var results = SetupResults<ResultTaskList>(new[] { new[] { _good, _good }, new[] { _good, _good } });
            var succeeded = InspectBatchResults(results);
            Assert.Equal(4, succeeded);
        }

        [Fact]
        public void It_fails_bad_batch_results()
        {
            var results = SetupResults<ResultTaskList>(new[] { new[] { _good, _good }, new[] { _good, _bad, _good } });
            Assert.Throws<DbUpdateException>(() => InspectBatchResults(results));
        }

        [Fact]
        public void It_throws_batch_exception()
        {
            var exceptedBatch = new TaskCompletionSource<ResultTaskList>();
            exceptedBatch.SetException(new AggregateException());
            Assert.Throws<AggregateException>(() => InspectBatchResults(new[] { exceptedBatch.Task }));
        }

        [Fact]
        public void It_counts_results()
        {
            var results = SetupResults<TableResult>(new[] { _good, _good });
            var succeeded = InspectResults(results);
            Assert.Equal(2, succeeded);
        }
        [Fact]
        public void It_throws_exception()
        {
            var exceptedBatch = new TaskCompletionSource<TableResult>();
            exceptedBatch.SetException(new AggregateException());
            Assert.Throws<AggregateException>(() => InspectResults(new[] { exceptedBatch.Task }));
        }
        [Fact]
        public void It_fails_bad_tasks()
        {
            var results = SetupResults<TableResult>(new[] { _good, _bad, _good });
            Assert.Throws<DbUpdateException>(() => InspectResults(results));
        }

        [Theory]
        [InlineData(EntityState.Added, TableOperationType.Insert)]
        [InlineData(EntityState.Modified, TableOperationType.Replace)]
        [InlineData(EntityState.Deleted, TableOperationType.Delete)]
        [InlineData(EntityState.Unknown, null)]
        [InlineData(EntityState.Unchanged, null)]
        public void It_maps_entity_state_to_table_operations(EntityState entityState, TableOperationType operationType)
        {
            var entry = new Mock<StateEntry>();
            entry.SetupGet(s => s.EntityState).Returns(entityState);
            entry.SetupGet(s => s.Entity).Returns(new TableEntity { ETag = "*" });
            var operation = GetOperation(entry.Object);

            if (operation == null)
            {
                Assert.True(EntityState.Unknown.HasFlag(entityState) || EntityState.Unchanged.HasFlag(entityState));
            }
            else
            {
                var propInfo = typeof(TableOperation).GetProperty("OperationType", BindingFlags.NonPublic | BindingFlags.Instance);
                var type = (TableOperationType)propInfo.GetValue(operation);
                Assert.Equal(operationType, type);
            }
        }
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(10)]
        public void It_ignores_invalid_entity_types(object obj)
        {
            var entry = new Mock<StateEntry>();
            entry.SetupGet(s => s.EntityState).Returns(EntityState.Added);
            entry.SetupGet(s => s.Entity).Returns(obj);
            Assert.Null(GetOperation(entry.Object));
        }

    }
}
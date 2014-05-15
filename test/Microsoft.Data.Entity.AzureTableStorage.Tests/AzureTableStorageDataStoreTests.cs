// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Data.Entity.ChangeTracking;
using Microsoft.Data.Entity.Infrastructure;
using Microsoft.WindowsAzure.Storage.Table;
using Moq;
using Xunit;

namespace Microsoft.Data.Entity.AzureTableStorage.Tests
{

    using ResultTaskList = IList<TableResult>;

    public class AzureTableStorageDataStoreTests : AzureStorageDataStore
    {
        private Task[] _failedTasks;
        private Task[] _succeededTasks;
        private Task[] _exceptedTasks;

        public AzureTableStorageDataStoreTests()
        {
            var excepted = new TaskCompletionSource<ResultTaskList>();
            excepted.SetException(new AggregateException());

            var failed = new TaskCompletionSource<ResultTaskList>();
            failed.SetResult(new[]
                {
                    new TableResult{HttpStatusCode = (int)HttpStatusCode.OK},
                    new TableResult{HttpStatusCode = (int)HttpStatusCode.BadRequest},
                    new TableResult{HttpStatusCode = (int)HttpStatusCode.OK},
                });
            var succeeded =new TaskCompletionSource<ResultTaskList>();
            succeeded.SetResult(new[]
                {
                    new TableResult{HttpStatusCode = (int)HttpStatusCode.OK},
                    new TableResult{HttpStatusCode = (int)HttpStatusCode.OK},
                });

            _failedTasks = new Task[] { succeeded.Task, failed.Task };
            _succeededTasks = new Task[] { succeeded.Task, succeeded.Task, };
            _exceptedTasks = new Task[] { succeeded.Task, excepted.Task };
        }

        [Fact]
        public void It_counts_results()
        {
            var succeeded = InspectBatchResults(_succeededTasks);
            Assert.Equal(4,succeeded);
        }

        [Fact]
        public void It_throws_exception()
        {
            Assert.Throws<AggregateException>(() => InspectBatchResults(_exceptedTasks));
        }

        [Fact]
        public void It_fails_bad_tasks()
        {
            Assert.Throws<AzureTableStorageException>(() => InspectBatchResults(_failedTasks));
        }

        [Theory]
        [InlineData(EntityState.Added, TableOperationType.Insert)]
        [InlineData(EntityState.Modified, TableOperationType.Replace)]
        [InlineData(EntityState.Deleted, TableOperationType.Delete)]
        [InlineData(EntityState.Unknown, null)]
        [InlineData(EntityState.Unchanged,null)]
        public void It_maps_entity_state_to_table_operations(EntityState entityState, TableOperationType operationType)
        {
            var entry = new Mock<StateEntry>();
            entry.SetupGet(s => s.EntityState).Returns(entityState);
            entry.SetupGet(s => s.Entity).Returns(new TableEntity{ETag = "*"});
            var operation = GetOperation(entry.Object);

            if (operation == null)
            {
                Assert.True(EntityState.Unknown.HasFlag(entityState) || EntityState.Unchanged.HasFlag(entityState));
            }
            else
            {
                var propInfo =  typeof(TableOperation).GetProperty("OperationType", BindingFlags.NonPublic | BindingFlags.Instance);
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
// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Net;
using Microsoft.Data.Entity.ChangeTracking;
using Microsoft.Data.Entity.Infrastructure;
using Microsoft.Data.Entity.Storage;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Remotion.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.Entity.AzureTableStorage
{
    public class AzureStorageDataStore : DataStore
    {
        private readonly AzureTableStorageConnection _connection;

        private const int MAX_BATCH_OPERATIONS = 100;

        public AzureStorageDataStore(DbContextConfiguration configuration, AzureTableStorageConnection connection)
            : base(configuration)
        {
            _connection = connection;
        }

        public override IEnumerable<TResult> Query<TResult>(QueryModel queryModel, StateManager stateManager)
        {
            var queryExecutor = new AzureTableStorageQueryModelVisitor().CreateQueryExecutor<TResult>(queryModel);
            var queryContext = new AzureTableStorageQueryContext(Model, Logger, stateManager, _connection);

            return queryExecutor(queryContext);
        }

        public override IAsyncEnumerable<TResult> AsyncQuery<TResult>(QueryModel queryModel, StateManager stateManager)
        {
            // TODO This should happen properly async
            return Query<TResult>(queryModel, stateManager).ToAsyncEnumerable();
        }

        public override Task<int> SaveChangesAsync(IReadOnlyList<StateEntry> stateEntries, CancellationToken cancellationToken = new CancellationToken())
        {
            var typeGroups = stateEntries.GroupBy(e => e.EntityType);
            var allBatchTasks = new List<Task>();

            var startBatch = new Action<TableBatchOperation, CloudTable>((batch, table) =>
            {
                var task = table.ExecuteBatchAsync(batch);
                allBatchTasks.Add(task);
            });

            foreach (var typeGroup in typeGroups)
            {
                var table = _connection.GetTableReference(typeGroup.Key.StorageName);
                var batch = new TableBatchOperation();

                foreach (var operation in typeGroup.Select(GetOperation).Where(operation => operation != null))
                {
                    batch.Add(operation);
                    if (batch.Count >= MAX_BATCH_OPERATIONS)
                    {
                        startBatch.Invoke(batch, table);
                        batch = new TableBatchOperation();
                    }
                }
                if (batch.Count != 0)
                {
                    startBatch.Invoke(batch, table);
                }
            }

            return new TaskFactory().ContinueWhenAll(allBatchTasks.ToArray(), t=>InspectBatchResults(t));
        }

        private static int InspectBatchResults(Task[] tasks)
        {
            var failedTask = tasks.FirstOrDefault(t => t.Exception != null);
            if (failedTask != default(Task))
            {
                Debug.Assert(failedTask.Exception != null, "failedTask.Exception != null");
                throw failedTask.Exception;
            }
            var changed = 0;
            foreach (var k in tasks)
            {
                var task = k as Task<IList<TableResult>>;
                if (task == null)
                {
                    continue;
                }
                var failedResult = task.Result.FirstOrDefault(result => result.HttpStatusCode >= (int)HttpStatusCode.BadRequest);
                if (failedResult != default(TableResult))
                {
                    throw new AzureTableStorageException("Could not add entity: " + failedResult.Result);
                }
                changed += task.Result.Count;
            }
            return changed;
        }

        private static TableOperation GetOperation(StateEntry entry)
        {
            TableOperation operation = null;
            var entity = (ITableEntity)entry.Entity;
            if (entity == null)
            {
                return null;
            }

            switch (entry.EntityState)
            {
                case EntityState.Added:
                    operation = TableOperation.Insert(entity);
                    break;

                case EntityState.Deleted:
                    operation = TableOperation.Delete(entity);
                    break;

                case EntityState.Modified:
                    operation = TableOperation.Replace(entity);
                    break;

                // noop
                case EntityState.Unchanged:
                case EntityState.Unknown:
                    break;

                default:
                    throw new NotImplementedException("Missing handler for new EntityState type");
            }
            return operation;
        }
    }
}

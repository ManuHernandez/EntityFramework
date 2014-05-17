// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Net;
using Microsoft.Data.Entity.AzureTableStorage;
using Microsoft.Data.Entity.AzureTableStorage.Interfaces;
using Microsoft.Data.Entity.AzureTableStorage.Wrappers;
using Microsoft.Data.Entity.ChangeTracking;
using Microsoft.Data.Entity.Infrastructure;
using Microsoft.Data.Entity.Metadata;
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
    public class AzureTableStorageDataStore : DataStore
    {
        private readonly AzureTableStorageConnection _connection;

        private const int MAX_BATCH_OPERATIONS = 100;

        /// <summary>
        /// Provided only for testing purposes. Do not use.
        /// </summary>
        protected AzureTableStorageDataStore(AzureTableStorageConnection connection)
        {
            _connection = connection;
        }

        public AzureTableStorageDataStore(DbContextConfiguration configuration, AzureTableStorageConnection connection)
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

        public override async Task<int> SaveChangesAsync(IReadOnlyList<StateEntry> stateEntries, CancellationToken cancellationToken = default(CancellationToken))
        {
            var tableGroups = stateEntries.GroupBy(s => s.EntityType);
            var allTasks = new List<Task<ITableResult>>();
            foreach (var tableGroup in tableGroups)
            {
                var table = _connection.GetTableReference(tableGroup.Key.StorageName);
                var tasks = tableGroup.Select(GetOperation)
                    .TakeWhile(operation => !cancellationToken.IsCancellationRequested)
                    .Select(operation => table.ExecuteAsync(operation, cancellationToken: cancellationToken));
                allTasks.AddRange(tasks);

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
            return await new TaskFactory<int>().ContinueWhenAll(allTasks.ToArray(), InspectResults);
        }

        protected int InspectResults(Task<ITableResult>[] tasks)
        {
            return CountTableResults(tasks, task =>
            {
                if (task.Result.HttpStatusCode >= HttpStatusCode.BadRequest)
                {
                    throw new DbUpdateException("Could not add entity: " + task.Result);
                }
                return 1;
            });
        }

        public async Task<int> SaveBatchChangesAsync(IReadOnlyList<StateEntry> stateEntries, CancellationToken cancellationToken = new CancellationToken())
        {
            var tableGroups = stateEntries.GroupBy(s => s.EntityType);
            var allBatchTasks = new List<Task<IList<ITableResult>>>();

            var startBatch = new Action<TableBatchOperation, ICloudTable>((batch, table) =>
            {
                // TODO allow user access to config options: Retry Policy, Secondary Storage, Timeout 
                var task = table.ExecuteBatchAsync(batch,cancellationToken: cancellationToken);
                allBatchTasks.Add(task);
            });

            foreach (var tableGroup in tableGroups)
            {
                var table = _connection.GetTableReference(tableGroup.Key.StorageName);
                var partitionGroups = tableGroup.GroupBy(s => (s.Entity as ITableEntity).PartitionKey);
                foreach (var partitionGroup in partitionGroups)
                {
                    var batch = new TableBatchOperation();
                    foreach (var operation in partitionGroup.Select(GetOperation).Where(operation => operation != null))
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }
                        //TODO An entity can only appear once in a transaction; Ensure that change tracker never returns multiple state entries for the same entity
                        batch.Add(operation);
                        if (batch.Count >= MAX_BATCH_OPERATIONS)
                        {
                            startBatch.Invoke(batch, table);
                            batch = new TableBatchOperation();
                        }
                    }
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    if (batch.Count != 0)
                    {
                        startBatch.Invoke(batch, table);
                    }
                }
            }
            return await new TaskFactory<int>().ContinueWhenAll(allBatchTasks.ToArray(), InspectBatchResults,cancellationToken);
        }

        protected int InspectBatchResults(Task<IList<ITableResult>>[] arg)
        {
            return CountTableResults(arg, task =>
                {
                    var failedResult = task.Result.FirstOrDefault(result => result.HttpStatusCode >= HttpStatusCode.BadRequest);
                    if (failedResult != default(ITableResult))
                    {
                        throw new DbUpdateException("Could not add entity: " + failedResult.Result);
                    }
                    return task.Result.Count;
                });
        }

        private int CountTableResults<TTask>(Task<TTask>[] tasks,Func<Task<TTask>,int> inspect)
        {
            var failedTask = tasks.FirstOrDefault(t => t.Exception != null);
            if (failedTask != null && failedTask.Exception != null)
            {
                throw failedTask.Exception;
            }
            //TODO identify failed tasks and their associated identity: return to user.
            return tasks.Aggregate(0, (current, task) => current + inspect(task));
        }



        protected TableOperation GetOperation(StateEntry entry)
        {
            TableOperation operation = null;
            var entity = entry.Entity as ITableEntity;
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
                    entity.ETag = entity.ETag ?? "*";
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

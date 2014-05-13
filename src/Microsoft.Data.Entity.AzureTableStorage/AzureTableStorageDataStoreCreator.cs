// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.Remoting.Messaging;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Storage;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.Entity.AzureTableStorage
{
    public class AzureTableStorageDataStoreCreator : DataStoreCreator
    {
        private readonly AzureTableStorageConnection _connection;

        public AzureTableStorageDataStoreCreator(AzureTableStorageConnection connection)
        {
            _connection = connection;
        }

        public override void Create(IModel model)
        {
            foreach (var type in model.EntityTypes)
            {
                var table = _connection.GetTableReference(type.StorageName);
                table.CreateIfNotExists();
            }
        }

        public override async Task CreateAsync(IModel model, CancellationToken cancellationToken = default(CancellationToken))
        {
            foreach (var type in model.EntityTypes)
            {
                var table = _connection.GetTableReference(type.StorageName);
                await table.CreateIfNotExistsAsync(cancellationToken);
            }
        }

        public override void Delete()
        {
            throw new NotImplementedException();
        }

        public override Task DeleteAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public override bool Exists()
        {
            throw new NotImplementedException();
        }


        public override Task<bool> ExistsAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
            //return Task.Factory.StartNew(()=>true, cancellationToken);
        }
    }
}

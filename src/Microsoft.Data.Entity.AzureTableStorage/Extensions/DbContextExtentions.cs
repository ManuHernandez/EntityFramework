// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.Data.Entity.AzureTableStorage.Extensions
{
    public static class DbContextExtentions
    {
        public static int SaveChangesAsBatch(this DbContext set)
        {
            //TODO implement connection between this call and the SaveChangesAsBatch call on the datastore
           throw new NotImplementedException(); 
        }
        public static int SaveChangesAsBatchAsync(this DbContext set)
        {
            //TODO implement connection between this call and the SaveChangesAsBatchAsync call on the datastore
            throw new NotImplementedException();
        }
    }
}
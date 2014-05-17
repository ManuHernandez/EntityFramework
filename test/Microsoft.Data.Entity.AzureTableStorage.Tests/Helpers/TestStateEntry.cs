// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Data.Entity.ChangeTracking;
using Microsoft.Data.Entity.Metadata;
using Microsoft.WindowsAzure.Storage.Table;
using Moq;

namespace Microsoft.Data.Entity.AzureTableStorage.Tests.Helpers
{
    public static class TestStateEntry
    {
        public static Mock<StateEntry> Mock()
        {
            var entry = new Mock<StateEntry>();
            entry.SetupGet(s => s.Entity).Returns(new TableEntity { ETag = "*" });
            return entry;
        }

        public static Mock<StateEntry> WithState(this Mock<StateEntry> mock, EntityState state)
        {
            mock.SetupGet(s => s.EntityState).Returns(() => state);
            return mock;
        }
        public static Mock<StateEntry> WithName(this Mock<StateEntry> mock, string name)
        {
            mock.SetupGet(s => s.EntityType).Returns(() => new EntityType(name));
            return mock;
        }
    }
}
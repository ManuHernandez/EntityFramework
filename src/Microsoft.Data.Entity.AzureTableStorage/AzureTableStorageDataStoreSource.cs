using Microsoft.Data.Entity.Infrastructure;
using Microsoft.Data.Entity.Storage;

namespace Microsoft.Data.Entity.AzureTableStorage
{
    class AzureTableStorageDataStoreSource : DataStoreSource<
        AzureStorageDataStore, 
        AzureTableStorageConfigurationExtension, 
        AzureTableStorageDataStoreCreator, 
        AzureTableStorageConnection>
    {
        public override bool IsAvailable(DbContextConfiguration configuration)
        {
            return IsConfigured(configuration);
        }

        public override string Name
        {
            get { return "AzureTableStorage"; }
        }
    }
}

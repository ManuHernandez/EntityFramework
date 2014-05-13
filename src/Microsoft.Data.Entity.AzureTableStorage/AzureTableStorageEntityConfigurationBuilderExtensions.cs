using Microsoft.Data.Entity.AzureTableStorage;

namespace Microsoft.Data.Entity
{
    public static class AzureTableStorageEntityConfigurationBuilderExtensions
    {
        public static DbContextOptions UseAzureTableStorge(this DbContextOptions builder, string connectionString)
        {
            builder.AddBuildAction(c => c.AddOrUpdateExtension<AzureTableStorageConfigurationExtension>(
                e => e.ConnectionString = connectionString));

            return builder;
        }
    }
}

using Microsoft.Data.Entity.Infrastructure;
using Microsoft.Framework.DependencyInjection;

namespace Microsoft.Data.Entity.AzureTableStorage
{
    public class AzureTableStorageConfigurationExtension : EntityConfigurationExtension
    {
        public string ConnectionString { get; set; }

        protected override void ApplyServices(EntityServicesBuilder builder)
        {
            builder.AddAzureTableStorage();
        }
    }
}

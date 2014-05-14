using System;

namespace Microsoft.Data.Entity.AzureTableStorage
{
    public class AzureTableStorageException : Exception
    {
        public AzureTableStorageException(string message, Exception innerException)
            :base(message,innerException)
        {
        }
    }
}
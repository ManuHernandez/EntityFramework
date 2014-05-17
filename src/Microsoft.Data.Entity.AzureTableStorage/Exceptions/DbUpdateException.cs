using System;

namespace Microsoft.Data.Entity.AzureTableStorage
{
    public class DbUpdateException : Exception
    {
        public DbUpdateException(string message, Exception innerException)
            :base(message,innerException)
        {
        }

        public DbUpdateException(string message)
            :base(message)
        {
        }
    }
}
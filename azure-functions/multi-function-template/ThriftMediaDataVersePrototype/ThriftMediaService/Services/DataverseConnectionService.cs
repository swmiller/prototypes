using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Text;

namespace ThriftMediaService.Services
{
    public class DataverseConnectionService : IDataverseConnectionService, IDisposable
    {
        private readonly ILogger<DataverseConnectionService> _logger;
        private readonly ServiceClient _serviceClient;

        public DataverseConnectionService(IConfiguration configuration, ILogger<DataverseConnectionService> logger)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var connectionString = configuration["DataverseConnectionString"];
            if (connectionString == null)
            {
                string errorMessage = "DataverseConnectionString is not configured.";
                _logger.LogError(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            _serviceClient = new ServiceClient(connectionString);
            if (!_serviceClient.IsReady)
            {
                string errorMessage = $"Failed to connect to Dataverse: {_serviceClient.LastError}";
                _logger.LogError(errorMessage, _serviceClient.LastError);
                throw new InvalidOperationException(errorMessage);
            }

            _logger.LogInformation("Successfully connected to Dataverse");
        }

        public void Dispose()
        {
            _serviceClient?.Dispose();
            GC.SuppressFinalize(this);
        }

        public IOrganizationServiceAsync2 GetService()
        {
            return _serviceClient;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;

namespace ThriftMediaService.Services
{
    public interface IDataverseConnectionService
    {
        IOrganizationServiceAsync2 GetService();
    }
}

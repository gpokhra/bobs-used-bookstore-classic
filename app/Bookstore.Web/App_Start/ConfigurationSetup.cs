using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Microsoft.Extensions.Configuration;
using NLog;
using System;
using System.Collections.Generic;

namespace Bookstore.Web
{
    public static class ConfigurationSetup
    {
        public static void LoadAwsParameterStore(IConfigurationBuilder configBuilder)
        {
            try
            {
                using var ssmClient = new AmazonSimpleSystemsManagementClient();

                var parameterNames = new List<string>
                {
                    "/bookstore/Authentication/Cognito/LocalClientId",
                    "/bookstore/Authentication/Cognito/AppRunnerClientId",
                    "/bookstore/Authentication/Cognito/MetadataAddress",
                    "/bookstore/Authentication/Cognito/CognitoDomain",
                    "/bookstore/Files/BucketName",
                    "/bookstore/Files/CloudFrontDomain",
                    "/bookstore/Services/Authentication",
                    "/bookstore/Services/FileService",
                    "/bookstore/Services/ImageValidationService"
                };

                var request = new GetParametersRequest
                {
                    Names = parameterNames,
                    WithDecryption = true
                };

                var response = ssmClient.GetParametersAsync(request).GetAwaiter().GetResult();

                var inMemorySettings = new Dictionary<string, string>();
                foreach (var param in response.Parameters)
                {
                    // /bookstore/Files/BucketName → Files:BucketName (ASP.NET Core convention)
                    var key = param.Name
                        .TrimStart('/')
                        .Replace("bookstore/", string.Empty)
                        .Replace("/", ":");
                    inMemorySettings[key] = param.Value;
                }

                configBuilder.AddInMemoryCollection(inMemorySettings);
            }
            catch (Exception ex)
            {
                LogManager.GetCurrentClassLogger()
                    .Error(ex, "Failed to load parameters from SSM Parameter Store");
            }
        }
    }
}

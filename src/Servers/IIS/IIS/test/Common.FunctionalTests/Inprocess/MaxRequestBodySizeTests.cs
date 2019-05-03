// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Server.IIS;
using Microsoft.AspNetCore.Server.IIS.FunctionalTests.Utilities;
using Microsoft.AspNetCore.Server.IntegrationTesting;
using Microsoft.AspNetCore.Server.IntegrationTesting.IIS;
using Microsoft.AspNetCore.Testing.xunit;
using Xunit;

namespace Microsoft.AspNetCore.Server.IISIntegration.FunctionalTests
{
    [Collection(PublishedSitesCollection.Name)]
    public class MaxRequestBodySizeTests : IISFunctionalTestBase
    {
        public MaxRequestBodySizeTests(PublishedSitesFixture fixture) : base(fixture)
        {
        }

        [ConditionalFact]
        [RequiresNewHandler]
        public async Task MaxRequestBodySizeE2EWorks()
        {
            var deploymentParameters = Fixture.GetBaseDeploymentParameters();
            deploymentParameters.TransformArguments((a, _) => $"{a} DecreaseRequestLimit");

            var deploymentResult = await DeployAsync(deploymentParameters);

            var result = await deploymentResult.HttpClient.PostAsync("/ReadRequestBody", new StringContent("test"));
            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, result.StatusCode);
        }

        [ConditionalFact]
        [RequiresNewHandler]
        public async Task SetIISLimitMaxRequestBodySizeE2EWorks()
        {
            var deploymentParameters = Fixture.GetBaseDeploymentParameters();
            deploymentParameters.ServerConfigActionList.Add(
                (config, _) => {
                    config
                        .RequiredElement("system.webServer")
                        .GetOrAdd("security")
                        .GetOrAdd("requestFiltering")
                        .GetOrAdd("requestLimits", "maxAllowedContentLength", "1");
                });
            var deploymentResult = await DeployAsync(deploymentParameters);

            var result = await deploymentResult.HttpClient.PostAsync("/ReadRequestBody", new StringContent("test"));

            // IIS returns a 404 instead of a 413... 
            Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        }

        [ConditionalFact]
        [RequiresNewHandler]
        [RequiresIIS(IISCapability.PoolEnvironmentVariables)]
        public async Task SetIISLimitMaxRequestBodyLogsWarning()
        {
            var deploymentParameters = Fixture.GetBaseDeploymentParameters();
            deploymentParameters.ServerConfigActionList.Add(
                (config, _) => {
                    config
                        .RequiredElement("system.webServer")
                        .GetOrAdd("security")
                        .GetOrAdd("requestFiltering")
                        .GetOrAdd("requestLimits", "maxAllowedContentLength", "1");
                });
            var deploymentResult = await DeployAsync(deploymentParameters);

            var result = await deploymentResult.HttpClient.PostAsync("/DecreaseRequestLimit", new StringContent("1"));
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);

            Assert.Single(TestSink.Writes, w => w.Message == "Increasing the MaxRequestBodySize conflicts with the max value for IIS limit maxAllowedContentLength." +
                " HTTP requests that have a content length greater than maxAllowedContentLength will still be rejected by IIS." +
                " You can disable the limit by either removing or setting the maxAllowedContentLength value to a higher limit.");
        }
    }
}

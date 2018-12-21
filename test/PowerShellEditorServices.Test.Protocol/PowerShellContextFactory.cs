//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.IO;
using System.Management.Automation.Host;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerShell.EditorServices.Console;
using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Test.Protocol
{
    internal static class PowerShellContextFactory
    {
        public static readonly HostDetails TestHostDetails =
            new HostDetails(
                "PowerShell Editor Services Test Host",
                "Test.PowerShellEditorServices",
                new Version("1.0.0"));

        // NOTE: These paths are arbitrarily chosen just to verify that the profile paths
        //       can be set to whatever they need to be for the given host.

        public static readonly ProfilePaths TestProfilePaths =
            new ProfilePaths(
                TestHostDetails.ProfileId,
                    Path.GetFullPath(
                        @"..\..\..\..\PowerShellEditorServices.Test.Shared\Profile"),
                    Path.GetFullPath(
                        @"..\..\..\..\PowerShellEditorServices.Test.Shared"));

        public static PowerShellContext Create(ILogger logger)
        {
            PowerShellContext powerShellContext = new PowerShellContext(logger);
            powerShellContext.Initialize(
                TestProfilePaths,
                PowerShellContext.CreateRunspace(
                    TestHostDetails,
                    powerShellContext,
                    new TestPSHostUserInterface(powerShellContext, logger),
                    logger),
                true);

            return powerShellContext;
        }
    }

    public class TestPSHostUserInterface : EditorServicesPSHostUserInterface
    {
        public TestPSHostUserInterface(
            PowerShellContext powerShellContext,
            ILogger logger)
            : base(
                powerShellContext,
                new SimplePSHostRawUserInterface(logger),
                Logging.NullLogger)
        {
        }

        public override void WriteOutput(string outputString, bool includeNewLine, OutputType outputType, ConsoleColor foregroundColor, ConsoleColor backgroundColor)
        {
        }

        protected override ChoicePromptHandler OnCreateChoicePromptHandler()
        {
            throw new NotImplementedException();
        }

        protected override InputPromptHandler OnCreateInputPromptHandler()
        {
            throw new NotImplementedException();
        }

        protected override Task<string> ReadCommandLine(CancellationToken cancellationToken)
        {
            return Task.FromResult("USER COMMAND");
        }

        protected override void UpdateProgress(long sourceId, ProgressDetails progressDetails)
        {
        }
    }
}

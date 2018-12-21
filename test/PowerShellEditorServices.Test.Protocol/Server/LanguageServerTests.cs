//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.PowerShell.EditorServices.Protocol.LanguageServer;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol;
using Microsoft.PowerShell.EditorServices.Session;
using Microsoft.PowerShell.EditorServices.Utility;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

using System.IO;
using Microsoft.PowerShell.EditorServices.Protocol.MessageProtocol.Serializers;

namespace Microsoft.PowerShell.EditorServices.Test.Protocol.Server
{
    internal class MockEditorSession: EditorSession
    {
        public MockEditorSession(): base(Logging.NullLogger)
        {
            this.StartSession(
                PowerShellContextFactory.Create(Logging.NullLogger),
                null
            );
        }
    }

    internal class NullRequestContext<TResult>: IRequestContext<TResult>
    {
        public List<TResult> Results { get; set; }

        public NullRequestContext()
        {
            Results = new List<TResult>();
        }

        public async Task SendResult(TResult resultDetails)
        {
            Results.Add(resultDetails);
            return;
        }

        public Task SendEvent<TParams, TRegistrationOptions>(NotificationType<TParams, TRegistrationOptions> eventType, TParams eventParams)
        {
            return null;
        }

        public Task SendError(object errorDetails)
        {
            return null;
        }
    }

    internal class MockLanguageServer: Microsoft.PowerShell.EditorServices.Protocol.Server.LanguageServer
    {
        public MockLanguageServer(): base (
            new MockEditorSession(),
            null,
            null,
            null,
            Logging.NullLogger
        )
        {
        }

        // Force previously hidden methods to be public by wrapping them
        public async Task PublicHandleFoldingRangeRequestAsync(
            FoldingRangeParams foldingParams,
            IRequestContext<FoldingRange[]> requestContext)
        {
            await this.HandleFoldingRangeRequestAsync(foldingParams, requestContext);
        }
    }

    public class LanguageServerTests
    {
        private MockLanguageServer subject;

        public LanguageServerTests() {
            subject = new MockLanguageServer();
        }

        /// <summary>
        /// Helper to create a FoldingRange request and prepopulate the workspace
        /// with the test file content
        /// </summary>
        private FoldingRangeParams CreateFoldingRequest(
            MockLanguageServer server,
            string content)
        {
            string filePath = "foldingrequest.ps1";

            server.editorSession.Workspace.CreateScriptFileFromFileBuffer(filePath, content);
            var request = new FoldingRangeParams();
            request.TextDocument = new TextDocumentIdentifier();
            request.TextDocument.Uri = filePath;
            return request;
        }

        /// <summary>
        /// Assertion helper to compare two FoldingRange arrays.
        /// </summary>
        private string FoldingRangeToString(FoldingRange value)
        {
            return $"StartLine = {value.StartLine}, StartCharacter = {value.StartCharacter}, " +
                    $"EndLine = {value.EndLine}, EndCharacter = {value.EndCharacter}, Kind = {value.Kind}";
        }

        /// <summary>
        /// Assertion helper to compare two FoldingRange arrays.
        /// </summary>
        private void AssertFoldingRangeArrays(
            FoldingRange[] expected,
            FoldingRange[] actual)
        {
            for (int index = 0; index < expected.Length; index++)
            {
                // System.Console.WriteLine(FoldingRangeToString(index, expected[index]));
                // System.Console.WriteLine(FoldingRangeToString(index, actual[index]));
                Assert.Equal(
                    FoldingRangeToString(expected[index]),
                    FoldingRangeToString(actual[index])
                    );
            }
            Assert.Equal(expected.Length, actual.Length);
        }

        /// <summary>
        /// Helper method to create FoldingRange objects with less typing
        /// </summary>
        private static FoldingRange CreateFoldingRange(int startLine, int startCharacter, int endLine, int endCharacter, string matchKind) {
            return new FoldingRange {
                StartLine      = startLine,
                StartCharacter = startCharacter,
                EndLine        = endLine,
                EndCharacter   = endCharacter,
                Kind           = matchKind
            };
        }

        [Trait("Category", "Folding")]
        [Fact]
        public async Task LaguageServerFindsFoldablRegionsWithMismatchedRegions()
        {
            string testString =
@"#endregion should not fold - mismatched

#region This should fold
$something = 'foldable'
#endregion

#region should not fold - mismatched
";
            FoldingRange[] expectedFolds = {
                CreateFoldingRange(2, 0, 3, 10, "region")
            };

            var context = new NullRequestContext<FoldingRange[]>();
            await subject.PublicHandleFoldingRangeRequestAsync(CreateFoldingRequest(subject, testString), context);

            Assert.Single(context.Results);
            AssertFoldingRangeArrays(expectedFolds, context.Results[0]);
            return;
        }


        [Trait("Category", "Folding")]
        [Fact]
        public async Task LaguageServiceFindsFoldablRegionsWithDuplicateRegions() {
            string testString =
@"# This script causes duplicate/overlapping ranges due to the `(` and `{` characters
$AnArray = @(Get-ChildItem -Path C:\ -Include *.ps1 -File).Where({
    $_.FullName -ne 'foo'}).ForEach({
        # Do Something
})
";
            FoldingRange[] expectedFolds = {
                CreateFoldingRange(1, 64, 1, 27, null),
                CreateFoldingRange(2, 35, 3,  2, null)
            };

            var context = new NullRequestContext<FoldingRange[]>();
            await subject.PublicHandleFoldingRangeRequestAsync(CreateFoldingRequest(subject, testString), context);

            Assert.Single(context.Results);
            AssertFoldingRangeArrays(expectedFolds, context.Results[0]);
            return;
        }

        // This tests that token matching { -> }, @{ -> } and
        // ( -> ), @( -> ) and $( -> ) does not confuse the folder
        [Fact]
        public async Task LaguageServiceFindsFoldablRegionsWithSameEndToken() {
            string testString =
@"foreach ($1 in $2) {

    $x = @{
        'abc' = 'def'
    }
}

$y = $(
    $arr = @('1', '2'); Write-Host ($arr)
)
";
            FoldingRange[] expectedFolds = {
                CreateFoldingRange(0, 19, 4, 1, null),
                CreateFoldingRange(2,  9, 3, 5, null),
                CreateFoldingRange(7,  5, 8, 1, null)
            };

            var context = new NullRequestContext<FoldingRange[]>();
            await subject.PublicHandleFoldingRangeRequestAsync(CreateFoldingRequest(subject, testString), context);

            Assert.Single(context.Results);
            AssertFoldingRangeArrays(expectedFolds, context.Results[0]);
            return;
        }

        // A simple PowerShell Classes test
        [Trait("Category","Foldingxx")]
        [Fact]
        public async Task LaguageServiceFindsFoldablRegionsWithClasses() {
            string testString =
@"class TestClass {
    [string[]] $TestProperty = @(
        'first',
        'second',
        'third')

    [string] TestMethod() {
        return $this.TestProperty[0]
    }
}
";
            FoldingRange[] expectedFolds = {
                CreateFoldingRange(0, 0, 10,  1, null),
                CreateFoldingRange(1, 31, 5, 16, null),
                CreateFoldingRange(6, 26, 9,  5, null)
            };

            var context = new NullRequestContext<FoldingRange[]>();
            await subject.PublicHandleFoldingRangeRequestAsync(CreateFoldingRequest(subject, testString), context);

            Assert.Single(context.Results);
            AssertFoldingRangeArrays(expectedFolds, context.Results[0]);
            return;
        }

        // This tests DSC style keywords and param blocks
        [Fact]
        public async Task LaguageServiceFindsFoldablRegionsWithDSC() {
            string testString =
@"Configuration Example
{
    param
    (
        [Parameter()]
        [System.String[]]
        $NodeName = 'localhost',

        [Parameter(Mandatory = $true)]
        [ValidateNotNullorEmpty()]
        [System.Management.Automation.PSCredential]
        $Credential
    )

    Import-DscResource -Module ActiveDirectoryCSDsc

    Node $AllNodes.NodeName
    {
        WindowsFeature ADCS-Cert-Authority
        {
            Ensure = 'Present'
            Name   = 'ADCS-Cert-Authority'
        }

        AdcsCertificationAuthority CertificateAuthority
        {
            IsSingleInstance = 'Yes'
            Ensure           = 'Present'
            Credential       = $Credential
            CAType           = 'EnterpriseRootCA'
            DependsOn        = '[WindowsFeature]ADCS-Cert-Authority'
        }
    }
}
";
            FoldingRange[] expectedFolds = {
                CreateFoldingRange(1,  0, 23, 1, null),
                CreateFoldingRange(3,  4, 12, 5, null),
                CreateFoldingRange(17, 4, 32, 5, null),
                CreateFoldingRange(19, 8, 22, 9, null),
                CreateFoldingRange(25, 8, 31, 9, null)
            };

            var context = new NullRequestContext<FoldingRange[]>();
            await subject.PublicHandleFoldingRangeRequestAsync(CreateFoldingRequest(subject, testString), context);

            Assert.Single(context.Results);
            AssertFoldingRangeArrays(expectedFolds, context.Results[0]);
            return;
        }



    }
}


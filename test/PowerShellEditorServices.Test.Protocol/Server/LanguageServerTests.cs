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
        /// with the a unique test file and content
        /// </summary>
        private FoldingRangeParams CreateFoldingRequest(
            MockLanguageServer server,
            string content)
        {
            string filePath = "foldingrequest-" + Guid.NewGuid().ToString() + ".ps1";

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

        internal class FoldingRangeComparer : IComparer<FoldingRange>
        {
            public int Compare(FoldingRange x, FoldingRange y)
            {
                return x.StartLine.CompareTo(y.StartLine);
            }
        }

        /// <summary>
        /// Assertion helper to compare two FoldingRange arrays.
        /// </summary>
        private void AssertFoldingRangeArrays(
            FoldingRange[] expected,
            FoldingRange[] actual)
        {
            // The foldable regions need to be deterministic for testing so sort the array.
            Array.Sort(actual, new FoldingRangeComparer());

            for (int index = 0; index < expected.Length; index++)
            {
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

        // This PowerShell script will exercise all of the
        // folding regions and regions which should not be
        // detected.  Due to file encoding this could be CLRF or LF line endings
        private const string allInOneScript =
@"#Region This should fold
<#
Nested different comment types.  This should fold
#>
#EndRegion

# region This should not fold due to whitespace
$shouldFold = $false
#    endRegion
function short-func-not-fold {};
<#
.SYNOPSIS
  This whole comment block should fold, not just the SYNOPSIS
.EXAMPLE
  This whole comment block should fold, not just the EXAMPLE
#>
function New-VSCodeShouldFold {
<#
.SYNOPSIS
  This whole comment block should fold, not just the SYNOPSIS
.EXAMPLE
  This whole comment block should fold, not just the EXAMPLE
#>
  $I = @'
herestrings should fold

'@

# This won't confuse things
Get-Command -Param @I

$I = @""
double quoted herestrings should also fold

""@

  # this won't be folded

  # This block of comments should be foldable as a single block
  # This block of comments should be foldable as a single block
  # This block of comments should be foldable as a single block

  #region This fools the indentation folding.
  Write-Host ""Hello""
    #region Nested regions should be foldable
    Write-Host ""Hello""
    # comment1
    Write-Host ""Hello""
    #endregion
    Write-Host ""Hello""
    # comment2
    Write-Host ""Hello""
    #endregion

  $c = {
    Write-Host ""Script blocks should be foldable""
  }

  # Array fools indentation folding
  $d = @(
  'should fold1',
  'should fold2'
  )
}

# Make sure contiguous comment blocks can be folded properly

# Comment Block 1
# Comment Block 1
# Comment Block 1
#region Comment Block 3
# Comment Block 2
# Comment Block 2
# Comment Block 2
$something = $true
#endregion Comment Block 3

# What about anonymous variable assignment
${this
is
valid} = 5

#RegIon This should fold due to casing
$foo = 'bar'
#EnDReGion
";
        private FoldingRange[] expectedAllInOneScriptFolds = {
            CreateFoldingRange(0,   0,  3, 10, "region"),
            CreateFoldingRange(1,   0,  2,  2, "comment"),
            CreateFoldingRange(10,  0, 14,  2, "comment"),
            CreateFoldingRange(16, 30, 62,  1, null),
            CreateFoldingRange(17,  0, 21,  2, "comment"),
            CreateFoldingRange(23,  7, 25,  2, null),
            CreateFoldingRange(31,  5, 33,  2, null),
            CreateFoldingRange(38,  2, 39,  0, "comment"),
            CreateFoldingRange(42,  2, 51, 14, "region"),
            CreateFoldingRange(44,  4, 47, 14, "region"),
            CreateFoldingRange(54,  7, 55,  3, null),
            CreateFoldingRange(59,  7, 61,  3, null),
            CreateFoldingRange(67,  0, 68,  0, "comment"),
            CreateFoldingRange(70,  0, 74, 26, "region"),
            CreateFoldingRange(71,  0, 72,  0, "comment"),
            CreateFoldingRange(78,  0, 79,  6, null),
        };

        [Trait("Category", "Folding")]
        [Fact]
        public async Task LaguageServiceFindsFoldablRegionsWithLF() {
            // Remove and CR characters
            string testString = allInOneScript.Replace("\r", "");
            // Ensure that there are no CR characters in the string
            Assert.True(testString.IndexOf("\r\n") == -1, "CRLF should not be present in the test string");

            var context = new NullRequestContext<FoldingRange[]>();
            await subject.PublicHandleFoldingRangeRequestAsync(CreateFoldingRequest(subject, testString), context);

            Assert.Single(context.Results);
            AssertFoldingRangeArrays(expectedAllInOneScriptFolds, context.Results[0]);
            return;
        }

        [Trait("Category", "Folding")]
        [Fact]
        public async Task LaguageServiceFindsFoldablRegionsWithCRLF() {
            // The Foldable regions should be the same regardless of line ending type
            // Enforce CRLF line endings, if none exist
            string testString = allInOneScript;
            if (testString.IndexOf("\r\n") == -1) {
                testString = testString.Replace("\n", "\r\n");
            }
            // Ensure that there are CRLF characters in the string
            Assert.True(testString.IndexOf("\r\n") != -1, "CRLF should be present in the teststring");

            var context = new NullRequestContext<FoldingRange[]>();
            await subject.PublicHandleFoldingRangeRequestAsync(CreateFoldingRequest(subject, testString), context);

            Assert.Single(context.Results);
            AssertFoldingRangeArrays(expectedAllInOneScriptFolds, context.Results[0]);
            return;
        }

        [Trait("Category", "Foldingx")]
        [Fact]
        public async Task LaguageServiceFindsFoldablRegionsWithoutLastLine() {
            // Increment the end line of the expected regions by one as we will
            // be hiding the last line
            FoldingRange[] expectedFolds = expectedAllInOneScriptFolds.Clone() as FoldingRange[];
            for (int index = 0; index < expectedFolds.Length; index++)
            {
                expectedFolds[index].EndLine++;
            }

            var context = new NullRequestContext<FoldingRange[]>();

int endLineOffset = this.currentSettings.CodeFolding.ShowLastLine ?

            await subject.PublicHandleFoldingRangeRequestAsync(CreateFoldingRequest(subject, allInOneScript), context);

            Assert.Single(context.Results);
            AssertFoldingRangeArrays(expectedFolds, context.Results[0]);
            return;
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
        [Trait("Category","Folding")]
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
                CreateFoldingRange(0, 16, 8,  1, null),
                CreateFoldingRange(1, 31, 3, 16, null),
                CreateFoldingRange(6, 26, 7,  5, null)
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


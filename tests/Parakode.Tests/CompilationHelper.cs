using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace SourceGeneratorSamples.Tests
{
    internal static class CompilationHelper
    {
        public static Assembly BuildAssembly(this Compilation compilation, string assemblyName)
        {
            using var ms = new MemoryStream();
            var emitResult = compilation.Emit(ms);
            emitResult.Diagnostics.Where(x => x.Severity > DiagnosticSeverity.Warning).Should().BeEmpty();
            emitResult.Success.Should().BeTrue();

            return Assembly.Load(ms.ToArray());
        }
    }
}

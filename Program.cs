using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Quantum.QsCompiler.Diagnostics;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Diagnostic = Microsoft.VisualStudio.LanguageServer.Protocol.Diagnostic;
using DiagnosticSeverity = Microsoft.VisualStudio.LanguageServer.Protocol.DiagnosticSeverity;
using Microsoft.Quantum.QsCompiler;
using Microsoft.Quantum.QsCompiler.CsharpGeneration;
using Microsoft.Quantum.QsCompiler.Transformations.BasicTransformations;
using Microsoft.Quantum.QsCompiler.DataTypes;
using System.Linq;
using Microsoft.Quantum.QsCompiler.SyntaxTree;

namespace Strathweb.Samples.BlazorQSharpInteractive
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");

            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

            await builder.Build().RunAsync();
        }
    }

     public class ConsoleLogger : LogTracker 
    {
        private readonly Func<Diagnostic, string> applyFormatting;

        protected internal virtual string Format(Diagnostic msg) =>
            this.applyFormatting(msg);

        protected sealed override void Print(Diagnostic msg) =>
            PrintToConsole(msg.Severity, this.Format(msg));

        public ConsoleLogger(
            Func<Diagnostic, string> format = null,
            DiagnosticSeverity verbosity = DiagnosticSeverity.Hint,
            IEnumerable<int> noWarn = null,
            int lineNrOffset = 0)
        : base(verbosity, noWarn, lineNrOffset) =>
            this.applyFormatting = format ?? Formatting.HumanReadableFormat;

        private static void PrintToConsole(DiagnosticSeverity severity, string message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            Console.WriteLine(severity.ToString() + " " + message);
        }
    }

    class InMemoryEmitter : IRewriteStep
    {
        public static Dictionary<string, string> GeneratedFiles { get; } = new Dictionary<string, string>();

        private readonly Dictionary<string, string> _assemblyConstants = new Dictionary<string, string>();
        private readonly List<IRewriteStep.Diagnostic> _diagnostics = new List<IRewriteStep.Diagnostic>();

        public string Name => "InMemoryCsharpGeneration";

        public int Priority => -2;

        public IDictionary<string, string> AssemblyConstants => _assemblyConstants;

        public IEnumerable<IRewriteStep.Diagnostic> GeneratedDiagnostics => _diagnostics;

        public bool ImplementsPreconditionVerification => false;

        public bool ImplementsTransformation => true;

        public bool ImplementsPostconditionVerification => false;

        public bool PreconditionVerification(QsCompilation compilation)
        {
            throw new NotImplementedException();
        }

        public bool Transformation(QsCompilation compilation, out QsCompilation transformed)
        {
            var context = CodegenContext.Create(compilation, _assemblyConstants);
            var sources = GetSourceFiles.Apply(compilation.Namespaces);

            foreach (var source in sources.Where(s => !s.Value.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
            {
                var content = SimulationCode.generate(source, context);
                GeneratedFiles.Add(source.Value, content);
            }

            if (!compilation.EntryPoints.IsEmpty)
            {
                var callable = context.allCallables.First(c => c.Key == compilation.EntryPoints.First()).Value;
                var content = EntryPoint.generate(context, callable);
                NonNullable<string> entryPointName = NonNullable<string>.New(callable.SourceFile.Value + ".EntryPoint");
                GeneratedFiles.Add(entryPointName.Value, content);
            }

            transformed = compilation;
            return true;
        }

        public bool PostconditionVerification(QsCompilation compilation)
        {
            throw new NotImplementedException();
        }
    }
}

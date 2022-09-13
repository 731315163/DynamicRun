using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace DynamicRun.Builder
{
    public class Compiler : IDisposable
    {
        protected IList<WeakReference> scop = new List<WeakReference>();
        private bool disposedValue;

        public Assembly? CreateAssembly(string filepath, string? referencepath = null, string? assemlyname = null, LanguageVersion version = LanguageVersion.CSharp10, OutputKind outputkind = OutputKind.DynamicallyLinkedLibrary, OptimizationLevel optimizationLevel = OptimizationLevel.Release)
        {
            return CreateAssembly(new FileStream(filepath, FileMode.Open), assemlyname ?? Path.GetFileNameWithoutExtension(filepath) + ".dll", referencepath, version, outputkind, optimizationLevel);
        }
        public Assembly? CreateAssembly(byte[] sourceCode, string assemlyname, string? referencepath = null, LanguageVersion version = LanguageVersion.CSharp10, OutputKind outputkind = OutputKind.DynamicallyLinkedLibrary, OptimizationLevel optimizationLevel = OptimizationLevel.Release)
        {
            using var asm = this.Compile(sourceCode, assemlyname, version, outputkind, optimizationLevel);
            if (asm == null)
                return null;
            asm.Seek(0, SeekOrigin.Begin);
            if (asm is MemoryStream)
            {
                return Assembly.Load((asm as MemoryStream).ToArray());
            }

            using var memory = new MemoryStream();
            asm.CopyTo(memory);
            return Assembly.Load(memory.ToArray());

        }
        public Assembly? CreateAssembly(Stream sourceCode, string assemlyname, string? referencepath = null, LanguageVersion version = LanguageVersion.CSharp10, OutputKind outputkind = OutputKind.DynamicallyLinkedLibrary, OptimizationLevel optimizationLevel = OptimizationLevel.Release)
        {
            using var asm = this.Compile(sourceCode, assemlyname, version, outputkind, optimizationLevel);
            if (asm == null)
                return null;
            asm.Seek(0, SeekOrigin.Begin);
            if (asm is MemoryStream)
            {
                return Assembly.Load((asm as MemoryStream).ToArray());
            }

            using var memory = new MemoryStream();
            asm.CopyTo(memory);
            return Assembly.Load(memory.ToArray());

        }
        public Assembly? CreateAssemblyWithAssemblyLoadContext(string filepath, string? referencepath = null, string? assemlyname = null, LanguageVersion version = LanguageVersion.CSharp10, OutputKind outputkind = OutputKind.DynamicallyLinkedLibrary, OptimizationLevel optimizationLevel = OptimizationLevel.Release)
        {
            return CreateAssemblyWithAssemblyLoadContext(new FileStream(filepath, FileMode.Open), assemlyname ?? Path.GetFileNameWithoutExtension(filepath) + ".dll", referencepath, version, outputkind, optimizationLevel);

        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public Assembly? CreateAssemblyWithAssemblyLoadContext(Stream sourceCode, string assemlyname, string? referencepath = null, LanguageVersion version = LanguageVersion.CSharp10, OutputKind outputkind = OutputKind.DynamicallyLinkedLibrary, OptimizationLevel optimizationLevel = OptimizationLevel.Release)
        {
            var asm = this.Compile(sourceCode, assemlyname, version, outputkind, optimizationLevel);
            using (asm)
            {
                var assemblyLoadContext = new UnloadableAssemblyLoadContext(referencepath);

                var assembly = assemblyLoadContext.LoadFromStream(asm);
                this.scop.Add(new WeakReference(assembly, trackResurrection: true));
                return assembly;

            }
        }
        public Stream Compile(byte[] sourceCode, string assemlyname, LanguageVersion version = LanguageVersion.CSharp10, OutputKind outputkind = OutputKind.DynamicallyLinkedLibrary, OptimizationLevel optimizationLevel = OptimizationLevel.Release)
        {
            Debug.WriteLine($"Starting compilation of: '{assemlyname}'");

            var compilation = GenerateCode(assemlyname, sourceCode, version, outputkind, optimizationLevel);
            return Compile(compilation);


        }
        public Stream Compile(string filepath, string? assemlyname = null, LanguageVersion version = LanguageVersion.CSharp10, OutputKind outputkind = OutputKind.DynamicallyLinkedLibrary, OptimizationLevel optimizationLevel = OptimizationLevel.Release)
        {

            Debug.WriteLine($"Starting compilation of: '{filepath}'");

            var sourceCode = new FileStream(filepath, FileMode.Open);
            return Compile(sourceCode, assemlyname ?? Path.GetFileNameWithoutExtension(filepath) + ".dll", version, outputkind, optimizationLevel);

        }
        public Stream Compile(Stream sourceCode, string assemlyname, LanguageVersion version = LanguageVersion.CSharp10, OutputKind outputkind = OutputKind.DynamicallyLinkedLibrary, OptimizationLevel optimizationLevel = OptimizationLevel.Release)
        {
            Debug.WriteLine($"Starting compilation of: '{assemlyname}'");

            var compilation = GenerateCode(assemlyname, sourceCode, version, outputkind, optimizationLevel);
            return Compile(compilation);

        }
        protected Stream Compile(CSharpCompilation compilation)
        {

            var peStream = new MemoryStream();

            var result = compilation.Emit(peStream);

            if (!result.Success)
            {
                Debug.WriteLine("Compilation done with error.");

                var failures = result.Diagnostics.Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);

                foreach (var diagnostic in failures)
                {
                    Debug.WriteLine("{0}: {1}", diagnostic.Id, diagnostic.GetMessage());
                }

                return Stream.Null;
            }

            Debug.WriteLine("Compilation done without any error.");

            peStream.Seek(0, SeekOrigin.Begin);

            return peStream;
        }
        public CSharpCompilation GenerateCode(string assemlyname, byte[] sourceCode, LanguageVersion version = LanguageVersion.CSharp10, OutputKind outputkind = OutputKind.DynamicallyLinkedLibrary, OptimizationLevel optimizationLevel = OptimizationLevel.Release)
        {
            var codeString = SourceText.From(sourceCode, sourceCode.Length);
            return GenerateCode(assemlyname, codeString, version, outputkind, optimizationLevel);
        }
        protected CSharpCompilation GenerateCode(string assemlyname, Stream sourceCode, LanguageVersion version = LanguageVersion.CSharp10, OutputKind outputkind = OutputKind.DynamicallyLinkedLibrary, OptimizationLevel optimizationLevel = OptimizationLevel.Release)
        {
            using (sourceCode)
            {
                var codeString = SourceText.From(sourceCode);
                return GenerateCode(assemlyname, codeString, version, outputkind, optimizationLevel);
            }
        }
        protected CSharpCompilation GenerateCode(string assemlyname, SourceText sourceCode, LanguageVersion version = LanguageVersion.CSharp10, OutputKind outputkind = OutputKind.DynamicallyLinkedLibrary, OptimizationLevel optimizationLevel = OptimizationLevel.Release)
        {

            var options = CSharpParseOptions.Default.WithLanguageVersion(version);

            var parsedSyntaxTree = SyntaxFactory.ParseSyntaxTree(sourceCode, options);

            var references = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            };

            Assembly.GetEntryAssembly()?.GetReferencedAssemblies().ToList()
                .ForEach(a => references.Add(MetadataReference.CreateFromFile(Assembly.Load(a).Location)));

            return CSharpCompilation.Create(assemlyname,
                new[] { parsedSyntaxTree },
                references: references,
                options: new CSharpCompilationOptions(outputkind,
                    optimizationLevel: optimizationLevel,
                    assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default));
        }



        public void Unload()
        {
            foreach (var wk in scop)
            {
                var target = wk.Target as AssemblyLoadContext;
                if (target != null)
                    target.Unload();

            }
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Unload();
                    foreach (var wk in scop)
                    {
                        for (int i = 0; wk.IsAlive && (i < 10); i++)
                        {
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                        }
                    }
                }

                // TODO: 释放未托管的资源(未托管的对象)并重写终结器
                // TODO: 将大型字段设置为 null
                disposedValue = true;
            }
        }

        // // TODO: 仅当“Dispose(bool disposing)”拥有用于释放未托管资源的代码时才替代终结器
        // ~Compiler()
        // {
        //     // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
        //     Dispose(disposing: false);
        // }

        void IDisposable.Dispose()
        {
            // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
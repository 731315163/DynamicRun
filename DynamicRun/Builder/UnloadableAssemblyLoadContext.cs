using System.Reflection;
using System.Runtime.Loader;
using System;
using System.Threading.Tasks;

namespace DynamicRun.Builder
{

    public class UnloadableAssemblyLoadContext : AssemblyLoadContext
    {
        private AssemblyDependencyResolver? _resolver;
        public static void Collect(WeakReference context)
        {
            for (int i = 0; context.IsAlive && (i < 10); i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
        public UnloadableAssemblyLoadContext(string? mainAssemblyToLoadPath = null) : base(isCollectible: true)
        {
            _resolver = string.IsNullOrEmpty(mainAssemblyToLoadPath) ? null : new AssemblyDependencyResolver(mainAssemblyToLoadPath);
        }

        protected override Assembly Load(AssemblyName name)
        {
            string? assemblyPath = _resolver?.ResolveAssemblyToPath(name);
            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }


    }
}
